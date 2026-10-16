using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public sealed partial class RowBuffersViewModel(CallStackNode node,
                                                CallStoreLookup lookup,
                                                CallTreeTabViewModel owner,
                                                ILogger logger,
                                                bool isDecoded) : ObservableObject, IDisposable
{
    private const int FixedColumns = 2;

    private const int CallWidth = 70;

    private const int ThreadWidth = 80;

    private const string SourceHeader = "Source";

    private const string ValueHeader = "Value";

    private const string ReferenceBadge = "Reference";

    private const string VariableLengthBadge = "Variable Length";

    private const string DictionaryBadge = "Dictionary";

    private const string TruncatedBadge = "Truncated";

    private static readonly Color SourceTint = Color.FromArgb(28, Color.LightGreen);


    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private QueryResultSet? _table;

    [ObservableProperty]
    private ResultRow<long>? _selectedRow;

    [ObservableProperty]
    private int _tableVersion;

    public CallStackNode Node { get; } = node;

    public string Title => $"{Node.Symbol} Row Buffers";

    public CallTreeTabViewModel Owner { get; } = owner;

    private CancellationTokenSource Cancellation { get; } = new();

    public bool IsSourceVisible { get; private set; }

    private bool IsDecoded { get; set; } = isDecoded;

    private int? RequestedCall { get; set; }

    public bool IsShowing(CallStackNode other)
        => Node.Frame is { } frame
           && other.Frame is { } otherFrame
           && frame.Address == otherFrame.Address
           && frame.Instance == otherFrame.Instance;

    public async Task LoadAsync()
    {
        if (Node.Frame is not { } frame)
        {
            return;
        }

        IsLoading = true;

        var token = Cancellation.Token;

        try
        {
            var table = await Task.Run(() => BuildTable(frame.Address, frame.Instance, token), token);

            Table = table;

            ApplyRequestedCall();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            logger.LogWarning(exception, "Reading the row buffers of {Function} failed", Node.Symbol);

            Message = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SetDecoded(bool decoded)
    {
        if (IsDecoded == decoded)
        {
            return;
        }

        IsDecoded = decoded;

        if (Table is not { Source: { } source })
        {
            return;
        }

        source.Invalidate();

        TableVersion++;
    }

    public void SetSourceVisible(bool isVisible)
    {
        if (IsSourceVisible == isVisible)
        {
            return;
        }

        IsSourceVisible = isVisible;

        _ = LoadAsync();
    }

    public void SelectCall(int call)
    {
        RequestedCall = call;

        ApplyRequestedCall();
    }

    public CallStackNode? GetCallNode(int call)
        => Node.Frame is { } frame
           && lookup.Store.GetCallList(frame.Address, frame.Instance) is { } calls
           && call >= 0
           && call < calls.Count
               ? lookup.Store.GetNode(calls[call])
               : null;

    partial void OnSelectedRowChanged(ResultRow<long>? value)
    {
        if (value is { Id: var call } && GetCallNode((int)call) is { } callNode)
        {
            Owner.Tree.ShowCall(callNode, (int)call);
        }
    }

    public void Dispose()
    {
        Cancellation.Cancel();

        Cancellation.Dispose();
    }

    private void ApplyRequestedCall()
    {
        if (RequestedCall is not { } call || Table is not { Rows: var rows })
        {
            return;
        }

        RequestedCall = null;

        var index = SortedSearch.FirstAtOrAfter(rows, (long)call, r => r.Id);

        if (index < rows.Count && rows[index].Id == call)
        {
            SelectedRow = rows[index];
        }
    }

    private QueryResultSet BuildTable(ulong address, ulong instance, CancellationToken cancellationToken)
    {
        var rowBuffers = lookup.RowBuffers.GetRows(address, instance);

        var callList = lookup.Store.GetCallList(address, instance);

        var width = lookup.RowBuffers.GetColumnCount(address, instance);

        var decoder = lookup.RowBufferDecoder;

        var first = rowBuffers.Count > 0 ? rowBuffers[0] : null;

        var isSourceVisible = IsSourceVisible;

        cancellationToken.ThrowIfCancellationRequested();

        var columns = new List<ResultColumn>
        {
            new(0, "Call", typeof(long), false) { Width = CallWidth, Alignment = ResultAlignment.Right },
            new(1, "Thread", typeof(uint), false) { Width = ThreadWidth, Alignment = ResultAlignment.Right }
        };

        for (var ordinal = 0; ordinal < width; ordinal++)
        {
            var name = first is not null && ordinal < first.Columns.Count
                ? RowBufferDisplay.GetLabel(first, first.Columns[ordinal], decoder)
                : $"Column {ordinal}";

            if (!isSourceVisible)
            {
                columns.Add(new ResultColumn(columns.Count, name, typeof(string), true));

                continue;
            }

            columns.Add(new ResultColumn(columns.Count, ValueHeader, typeof(string), true) { Group = name });

            columns.Add(new ResultColumn(columns.Count, SourceHeader, typeof(string), false)
            {
                BackgroundColour = SourceTint,
                Group = name
            });
        }

        var source = new CachedRowSource<RowBuffer>(rowBuffers,
                                                    columns.Count,
                                                    (row, _) => GetValues(callList,
                                                                          row,
                                                                          columns.Count,
                                                                          decoder,
                                                                          IsDecoded,
                                                                          isSourceVisible));

        var rows = new ResultRow<long>[rowBuffers.Count];

        for (var index = 0; index < rows.Length; index++)
        {
            rows[index] = new ResultRow<long>(source, index) { Id = rowBuffers[index].Call };
        }

        return new QueryResultSet { Columns = columns, Rows = rows, Source = source };
    }

    private object?[] GetValues(TraceCallList? callList,
                                RowBuffer row,
                                int fieldCount,
                                RowBufferDecoder? decoder,
                                bool decode,
                                bool isSourceVisible)
    {
        var values = new object?[fieldCount];

        var call = callList is not null && row.Call < callList.Count ? callList[row.Call] : null;

        values[0] = call is not null && lookup.Store.GetNode(call) is { } callNode
            ? new CallValue(callNode, row.Call)
            : (long)row.Call + 1;

        values[1] = call?.ThreadId;

        var stride = isSourceVisible ? 2 : 1;

        for (var ordinal = 0; ordinal < row.Columns.Count && FixedColumns + ordinal * stride < fieldCount; ordinal++)
        {
            var index = FixedColumns + ordinal * stride;

            values[index] = RowBufferDisplay.GetValue(row, row.Columns[ordinal], decoder, decode);

            if (isSourceVisible && index + 1 < fieldCount)
            {
                values[index + 1] = GetSource(row, row.Columns[ordinal]);
            }
        }

        return values;
    }

    private static bool IsVariableLength(RowBuffer row, RowBufferColumn column)
        => row.Format is RowBufferFormat.Bindings or RowBufferFormat.HashTable && column.Binding.RecordOffset < 0;

    private static string? GetDetail(RowBuffer row, RowBufferColumn column)
    {
        string? length = column.HasLength ? $"Length {column.Length.ToString(CultureInfo.InvariantCulture)}" : null;

        if (row.Format is not (RowBufferFormat.Bindings or RowBufferFormat.HashTable) || IsVariableLength(row, column))
        {
            return length;
        }

        var offset = $"Offset {column.Binding.RecordOffset.ToString(CultureInfo.InvariantCulture)}";

        return length is null ? offset : $"{offset}  {length}";
    }

    private static IReadOnlyList<string> GetBadges(RowBuffer row, RowBufferColumn column, RowBufferSource source)
    {
        var badges = new List<string>();

        if (source.IsFromDictionary)
        {
            badges.Add(DictionaryBadge);
        }

        if (IsVariableLength(row, column))
        {
            badges.Add(VariableLengthBadge);
        }

        if (column.IsTruncated)
        {
            badges.Add(TruncatedBadge);
        }

        if (column.UnreadableReason is { } reason)
        {
            badges.Add(reason);
        }

        return badges;
    }

    private BadgeText GetSource(RowBuffer row, RowBufferColumn column)
    {
        var source = lookup.RowBuffers.GetSource(row, column, lookup.Batches);

        return new BadgeText(column.IsByReference ? [ReferenceBadge] : [],
                             GetSourceLabel(source),
                             GetBadges(row, column, source),
                             GetDetail(row, column));
    }

    private string GetSourceLabel(RowBufferSource source) => source.Type switch
    {
        RowBufferSourceType.Inline => "Inline",
        RowBufferSourceType.BufferPool => "Buffer Pool",
        RowBufferSourceType.Lob => "LOB",
        RowBufferSourceType.RowBuffer => $"{GetIteratorLabel(source.Iterator)} +0x{source.Offset:X}",
        RowBufferSourceType.Batch => GetBatchLabel(source),
        RowBufferSourceType.HashTable => $"{GetIteratorLabel(source.Iterator)} Hash Table",
        _ => "Memory"
    };

    private string GetBatchLabel(RowBufferSource source)
    {
        var vector = source.Row < 0 ? $"Vector {source.Column}" : $"Vector {source.Column} Row {source.Row}";

        return source.Iterator == 0 ? vector : $"{GetIteratorLabel(source.Iterator)} {vector}";
    }

    private string GetIteratorLabel(ulong iterator) => lookup.GetIterator(iterator)?.Label ?? $"Iterator 0x{iterator:X}";
}
