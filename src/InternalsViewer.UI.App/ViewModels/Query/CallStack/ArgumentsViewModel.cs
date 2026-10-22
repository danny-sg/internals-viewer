using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.Extensions.Logging;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Memory;
using System.Linq;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public sealed partial class ArgumentsViewModel(ILogger logger, SymbolsViewModel symbols, Func<ulong, string?> getHoBtName)
    : ObservableObject, IDisposable
{
    private const string DefaultMessage = "Right click a frame in a Full Trace call tree and choose Show Arguments";

    [ObservableProperty]
    private string _signature = string.Empty;

    [ObservableProperty]
    private string? _message = DefaultMessage;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingMessage = "Loading...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCalls))]
    private QueryResultSet? _callTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedCall;

    [ObservableProperty]
    private CallReference? _selectedCallReference;

    [ObservableProperty]
    private int _callTableVersion;

    public bool HasCalls => CallTable is not null;

    public CallStackNode? Shown { get; private set; }

    public int? SelectedCallIndex => SelectedCall is { Id: var index } ? (int)index : null;

    public CallStoreLookup? Lookup { get; private set; }

    private CallExclusions Exclusions { get; set; } = CallExclusions.None;

    private bool IsDecoded { get; set; } = true;

    private IReadOnlyList<int>? Included { get; set; }

    private ArgumentLayout? Layout { get; set; }

    private IReadOnlyList<TraceArgumentCall> LoggedCalls { get; set; } = [];

    private CancellationTokenSource? Request { get; set; }

    private bool IsSelectingQuietly { get; set; }

    private bool IsQuietlySelected { get; set; }

    public event Action<CallReference>? CallSelected;

    public event Action? SourceChanged;

    public bool CanShow(CallStackNode node) => Lookup is not null && node.Frame is { Address: not 0, Pdb.Length: > 0 };

    public void SetSource(TraceCallStore? store,
                          CallStackTree? tree = null,
                          IReadOnlyDictionary<ulong, IteratorTarget>? iterators = null,
                          HoBtAccessMap? hobtAccess = null,
                          MemoryMap? memoryMap = null)
    {
        Cancel();

        Lookup = store is null
            ? null
            : new CallStoreLookup(store,
                                tree,
                                iterators ?? new Dictionary<ulong, IteratorTarget>(),
                                hobtAccess ?? HoBtAccessMap.Empty,
                                memoryMap ?? MemoryMap.Empty,
                                symbols,
                                getHoBtName);

        Shown = null;
        Signature = string.Empty;
        Message = DefaultMessage;
        Clear();
        IsLoading = false;

        SourceChanged?.Invoke();
    }

    public void SetExclusions(CallExclusions exclusions)
    {
        Exclusions = exclusions;

        if (Shown is { } shown && !IsLoading)
        {
            _ = LoadAsync(shown, SelectedCallIndex);
        }
    }

    public void SetDecoded(bool decoded)
    {
        if (IsDecoded == decoded)
        {
            return;
        }

        IsDecoded = decoded;

        if (CallTable is not { Source: { } source })
        {
            return;
        }

        source.Invalidate();

        CallTableVersion++;
    }

    public async Task ShowAsync(CallStackNode node)
    {
        if (IsShowing(node) && IsLoading)
        {
            return;
        }

        if (!IsShowing(node) || CallTable is null)
        {
            await LoadAsync(node, null);
        }

        Shown = node;

        SelectQuietly(null);
    }

    public Task ShowAsync(CallStackNode node, int? callIndex)
    {
        if (!IsShowing(node) || IsLoading || CallTable is not { } table)
        {
            return LoadAsync(node, callIndex);
        }

        var row = GetRow(table, callIndex);

        if (row is null && callIndex is >= 0)
        {
            return LoadAsync(node, callIndex);
        }

        Shown = node;

        SelectQuietly(row, selected: true);

        return Task.CompletedTask;
    }

    public ArgumentCell GetCell(int index, int ordinal)
        => Layout is { } layout && GetCall(index) is { } call ? ArgumentRowBuilder.GetCellAt(layout, call, ordinal) : default;

    public string? GetDecodedText(int index, int ordinal)
        => Layout is { } layout && Lookup is { } lookup && GetCall(index) is { } call
            ? ArgumentRowBuilder.DecodedTextAt(layout, call, ordinal, lookup.Describe)
            : null;

    public MemoryMapEntry? GetOrigin(int index, int ordinal)
        => Layout is { } layout && Lookup is { } lookup && GetCall(index) is { } call
            ? ArgumentRowBuilder.OriginAt(layout, call, ordinal, lookup.Describe)
            : null;

    public CallStackNode? GetNode(int index) => GetCall(index) is { } call ? Lookup?.Store.GetNode(call) : null;

    public (string Name, int Register)? GetHookArgument(int ordinal)
        => Layout is { } layout ? ArgumentRowBuilder.GetHookArgument(layout, ordinal) : null;

    public IteratorTarget? GetIterator(ulong value) => Lookup?.GetIterator(value);

    public ulong? GetHoBtId(int index, int ordinal)
    {
        if (Layout is not { } layout || Lookup is not { } lookup || GetCall(index) is not { } call)
        {
            return null;
        }

        if (ArgumentRowBuilder.FindSlotAt(layout, ordinal) is { } slot)
        {
            return call.GetValue(slot) is { } value ? lookup.GetHoBtId(slot, value, call) : null;
        }

        return ArgumentRowBuilder.GetCellAt(layout, call, ordinal) is { Value: { } returned, Type: { } type }
            ? ArgumentValue.GetHoBtId(type, returned)
            : null;
    }

    public void Dispose()
    {
        Cancel();

        Lookup = null;
        Shown = null;
        SelectedCallReference = null;
        Clear();
    }

    private async Task LoadAsync(CallStackNode node, int? select)
    {
        if (Lookup is not { } lookup || node.Frame is not { } frame)
        {
            return;
        }

        Cancel();

        var request = new CancellationTokenSource();

        Request = request;

        Shown = node;
        Signature = node.Symbol;
        Message = null;
        Clear();
        IsLoading = true;
        LoadingMessage = "Resolving Signature";

        try
        {
            var resolved = await lookup.ResolveAsync(frame);

            if (request.IsCancellationRequested)
            {
                return;
            }

            if (resolved is null)
            {
                Message = "The function's signature could not be resolved from its symbols";

                return;
            }

            var layout = resolved.Layout;

            Signature = GetHeader(resolved.Parsed, resolved.Text ?? node.Symbol);

            if (lookup.Store.GetCallList(frame.Address, frame.InstanceAddress) is not { Count: > 0 } calls)
            {
                Message = "The function was not called on the query's threads";

                return;
            }

            LoadingMessage = "Reading Call Log";

            var exclusions = Exclusions;

            var included = exclusions.IsEmpty
                ? null
                : Include(await Task.Run(() => lookup.Store.GetIncludedCalls(calls, exclusions.Includes, request.Token), request.Token),
                          select);

            if (included is { Count: 0 })
            {
                Message = "Every call of the function is excluded by the category filter";

                return;
            }

            var table = await Task.Run(() => ArgumentRowBuilder.BuildTable(layout, calls, lookup.Describe, included, () => IsDecoded),
                                       request.Token);

            Layout = layout;
            LoggedCalls = calls;
            Included = included;
            CallTable = table;

            if (GetRow(table, select) is { } row)
            {
                SelectQuietly(row, selected: true);
            }
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            logger.LogWarning(exception, "Reading the arguments of {Function} failed", Signature);

            Message = exception.Message;
        }
        finally
        {
            if (ReferenceEquals(Request, request))
            {
                IsLoading = false;
            }
        }
    }

    private TraceArgumentCall? GetCall(int index) => index >= 0 && index < LoggedCalls.Count ? LoggedCalls[index] : null;

    private static IReadOnlyList<int> Include(IReadOnlyList<int> included, int? callIndex)
    {
        if (callIndex is not { } index || index < 0)
        {
            return included;
        }

        var calls = included as List<int> ?? [.. included];

        var position = calls.BinarySearch(index);

        if (position < 0)
        {
            calls.Insert(~position, index);
        }

        return calls;
    }

    private static ResultRow<long>? GetRow(QueryResultSet table, int? callIndex)
    {
        if (callIndex is not { } target || target < 0)
        {
            return null;
        }

        var low = 0;

        var high = table.Rows.Count - 1;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;

            var row = (ResultRow<long>)table.Rows[middle];

            if (row.Id == target)
            {
                return row;
            }

            if (row.Id < target)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return null;
    }

    private void SelectQuietly(ResultRow<long>? row, bool selected = false)
    {
        IsSelectingQuietly = true;

        IsQuietlySelected = selected;

        try
        {
            SelectedCall = row;
        }
        finally
        {
            IsSelectingQuietly = false;

            IsQuietlySelected = false;
        }
    }

    private bool IsShowing(CallStackNode node)
        => Shown?.Frame is { } shown
           && node.Frame is { } frame
           && shown.Address == frame.Address
           && shown.InstanceAddress == frame.InstanceAddress;

    private static string GetHeader(FunctionSignature parsed, string signature)
    {
        var header = parsed.ReturnType is { } returnType ? $"{returnType} {signature}" : signature;

        return parsed.Type == FunctionType.Static ? $"static {header}" : header;
    }

    partial void OnSelectedCallChanged(ResultRow<long>? value)
    {
        var reference = value is { Id: var index } && GetNode((int)index) is { } node
            ? new CallReference(node, (int)index, !IsSelectingQuietly || IsQuietlySelected)
            : null;

        if (reference is { IsSelected: true } && !IsSelectingQuietly)
        {
            CallSelected?.Invoke(reference);
        }

        SelectedCallReference = reference;
    }

    private void Clear()
    {
        Layout = null;
        LoggedCalls = [];
        Included = null;
        SelectedCall = null;
        CallTable = null;
    }

    private void Cancel()
    {
        Request?.Cancel();

        Request?.Dispose();

        Request = null;
    }
}
