using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.Extensions.Logging;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Memory;

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

    public bool HasCalls => CallTable is not null;

    public CallStackNode? Shown { get; private set; }

    public int? SelectedCallIndex => SelectedCall is { Id: var index } ? (int)index : null;

    public CallLogLookup? Lookup { get; private set; }

    private CallExclusions Exclusions { get; set; } = CallExclusions.None;

    private bool IsDecoded { get; set; } = true;

    private IReadOnlyList<int>? Included { get; set; }

    private ArgumentLayout? Layout { get; set; }

    private IReadOnlyList<TraceArgumentCall> LoggedCalls { get; set; } = [];

    private CancellationTokenSource? Request { get; set; }

    private bool IsSelectingQuietly { get; set; }

    private bool IsChoosingQuietly { get; set; }

    public event Action<CallReference>? CallChosen;

    public event Action? SourceChanged;

    public bool CanShow(CallStackNode node) => Lookup is not null && node.Frame is { Address: not 0, Pdb.Length: > 0 };

    public void SetSource(TraceCallLog? log,
                          CallStackTree? tree = null,
                          IReadOnlyDictionary<ulong, IteratorTarget>? iterators = null,
                          HoBtAccessMap? hobtAccess = null,
                          MemoryMap? memoryMap = null)
    {
        Cancel();

        Lookup = log is null
            ? null
            : new CallLogLookup(log,
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

        if (Layout is not { } layout || Lookup is not { } lookup || CallTable is null)
        {
            return;
        }

        var selected = SelectedCallIndex;

        CallTable = ArgumentRowBuilder.Table(layout, LoggedCalls, lookup.Describe, Included, decoded);

        SelectQuietly(RowFor(CallTable, selected));
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

    public Task ShowAsync(CallStackNode node, int? call)
    {
        if (!IsShowing(node) || IsLoading || CallTable is not { } table)
        {
            return LoadAsync(node, call);
        }

        Shown = node;

        SelectQuietly(RowFor(table, call), chosen: true);

        return Task.CompletedTask;
    }

    public ArgumentCell CellAt(int index, int ordinal)
        => Layout is { } layout && index >= 0 && index < LoggedCalls.Count
            ? ArgumentRowBuilder.CellAt(layout, LoggedCalls[index], ordinal)
            : default;

    public string? DecodedTextAt(int index, int ordinal)
        => Layout is { } layout && Lookup is { } lookup && index >= 0 && index < LoggedCalls.Count
            ? ArgumentRowBuilder.DecodedTextAt(layout, LoggedCalls[index], ordinal, lookup.Describe)
            : null;

    public CallStackNode? NodeAt(int index)
        => index >= 0 && index < LoggedCalls.Count ? Lookup?.Log.NodeOf(LoggedCalls[index]) : null;

    public IteratorTarget? GetIterator(ulong value) => Lookup?.GetIterator(value);

    public ulong? HoBtIdAt(int index, int ordinal)
    {
        if (Layout is not { } layout || Lookup is not { } lookup || index < 0 || index >= LoggedCalls.Count)
        {
            return null;
        }

        var call = LoggedCalls[index];

        if (ArgumentRowBuilder.SlotAt(layout, ordinal) is { } slot)
        {
            return call.Value(slot) is { } value ? lookup.GetHoBtId(slot, value, call) : null;
        }

        return ArgumentRowBuilder.CellAt(layout, call, ordinal) is { Value: { } returned, Type: { } type }
            ? ArgumentValue.GetHoBtId(type, returned)
            : null;
    }

    public void Dispose() => Cancel();

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
            var signature = await symbols.ResolveFrameSignatureAsync(frame);

            var decoratedName = await symbols.ResolveFrameDecoratedNameAsync(frame);

            if (request.IsCancellationRequested)
            {
                return;
            }

            if (FunctionSignature.Parse(signature, decoratedName) is not { } parsed)
            {
                Message = "The function's signature could not be resolved from its symbols";

                return;
            }

            var layout = KnownParameters.Apply(ArgumentLayout.LayoutOf(parsed), frame.Resolved?.ClassName, frame.Resolved?.MethodName);

            Signature = Header(parsed, signature ?? node.Symbol);

            if (lookup.Log.CallsOf(frame.Address, frame.Instance) is not { Count: > 0 } calls)
            {
                Message = "The function was not called on the query's threads";

                return;
            }

            LoadingMessage = "Reading Call Log";

            var exclusions = Exclusions;

            var included = exclusions.IsEmpty
                ? null
                : await Task.Run(() => lookup.Log.IncludedCalls(calls, exclusions.Includes, request.Token), request.Token);

            if (included is { Count: 0 })
            {
                Message = "Every call of the function is excluded by the category filter";

                return;
            }

            var decoded = IsDecoded;

            var table = await Task.Run(() => ArgumentRowBuilder.Table(layout, calls, lookup.Describe, included, decoded), request.Token);

            Layout = layout;
            LoggedCalls = calls;
            Included = included;
            CallTable = table;

            if (RowFor(table, select) is { } row)
            {
                SelectQuietly(row, chosen: true);
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

    private static ResultRow<long>? RowFor(QueryResultSet table, int? call)
    {
        if (call is not { } target || target < 0)
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

    private void SelectQuietly(ResultRow<long>? row, bool chosen = false)
    {
        IsSelectingQuietly = true;

        IsChoosingQuietly = chosen;

        try
        {
            SelectedCall = row;
        }
        finally
        {
            IsSelectingQuietly = false;

            IsChoosingQuietly = false;
        }
    }

    private bool IsShowing(CallStackNode node)
        => Shown?.Frame is { } shown && node.Frame is { } frame && shown.Address == frame.Address && shown.Instance == frame.Instance;

    private static string Header(FunctionSignature parsed, string signature)
    {
        var header = parsed.ReturnType is { } returnType ? $"{returnType} {signature}" : signature;

        return parsed.Kind == FunctionKind.Static ? $"static {header}" : header;
    }

    partial void OnSelectedCallChanged(ResultRow<long>? value)
    {
        var reference = value is { Id: var index } && NodeAt((int)index) is { } node
            ? new CallReference(node, (int)index, !IsSelectingQuietly || IsChoosingQuietly)
            : null;

        if (reference is { IsChosen: true } && !IsSelectingQuietly)
        {
            CallChosen?.Invoke(reference);
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
