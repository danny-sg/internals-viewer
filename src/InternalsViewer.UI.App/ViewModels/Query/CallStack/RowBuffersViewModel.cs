using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.RowBuffers;
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

    private bool IsDecoded { get; set; } = isDecoded;

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

    private QueryResultSet BuildTable(ulong address, ulong instance, CancellationToken cancellationToken)
    {
        var calls = lookup.RowBuffers.GetCalls(address, instance);

        var callList = lookup.Store.GetCallList(address, instance);

        var width = lookup.RowBuffers.GetColumnCount(address, instance);

        var decoder = lookup.RowBufferDecoder;

        var first = calls.Count > 0 ? lookup.RowBuffers.GetRow(address, instance, calls[0]) : null;

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

            columns.Add(new ResultColumn(columns.Count, name, typeof(string), true));
        }

        var source = new CachedRowSource<int>(calls,
                                              columns.Count,
                                              (call, _) => GetValues(address, instance, callList, call, columns.Count, decoder, IsDecoded));

        var rows = new ResultRow<long>[calls.Count];

        for (var index = 0; index < rows.Length; index++)
        {
            rows[index] = new ResultRow<long>(source, index) { Id = calls[index] };
        }

        return new QueryResultSet { Columns = columns, Rows = rows, Source = source };
    }

    private object?[] GetValues(ulong address,
                                ulong instance,
                                TraceCallList? callList,
                                int rowCall,
                                int fieldCount,
                                RowBufferDecoder? decoder,
                                bool decode)
    {
        var values = new object?[fieldCount];

        var call = callList is not null && rowCall < callList.Count ? callList[rowCall] : null;

        values[0] = call is not null && lookup.Store.GetNode(call) is { } callNode
            ? new CallValue(callNode, rowCall)
            : (long)rowCall + 1;

        values[1] = call?.ThreadId;

        if (lookup.RowBuffers.GetRow(address, instance, rowCall) is not { } row)
        {
            return values;
        }

        for (var ordinal = 0; ordinal < row.Columns.Count && FixedColumns + ordinal < fieldCount; ordinal++)
        {
            values[FixedColumns + ordinal] = RowBufferDisplay.GetValue(row, row.Columns[ordinal], decoder, decode);
        }

        return values;
    }
}
