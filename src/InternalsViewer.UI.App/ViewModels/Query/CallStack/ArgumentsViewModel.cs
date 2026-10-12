using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public sealed partial class ArgumentsViewModel(ILogger logger, SymbolsViewModel symbols) : ObservableObject, IDisposable
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
    [NotifyPropertyChangedFor(nameof(HasValueSearch))]
    private string? _valueSearch;

    [ObservableProperty]
    private IReadOnlyList<ValueUseGroup> _valueGroups = [];

    [ObservableProperty]
    private string? _valueIdentity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueIteratorLabel))]
    private IteratorTarget? _valueIterator;

    [ObservableProperty]
    private bool _isSearching;

    public bool HasCalls => CallTable is not null;

    public bool HasValueSearch => ValueSearch is not null;

    public string? ValueIteratorLabel => ValueIterator is { } iterator
        ? $"Is The {iterator.Label} {(iterator.IsPlanObject ? "Plan Object" : "Iterator")}"
        : null;

    public CallStackNode? Shown { get; private set; }

    public int? SelectedCallIndex => SelectedCall is { Id: var index } ? (int)index : null;

    private TimeTravelCallLog? Log { get; set; }

    private IReadOnlyDictionary<ulong, IteratorTarget> Iterators { get; set; } = new Dictionary<ulong, IteratorTarget>();

    private ArgumentLayout? Layout { get; set; }

    private IReadOnlyList<TimeTravelArgumentCall> LoggedCalls { get; set; } = [];

    private CancellationTokenSource? Request { get; set; }

    private CancellationTokenSource? Search { get; set; }

    private bool IsSelectingQuietly { get; set; }

    private bool IsChoosingQuietly { get; set; }

    public event Action<CallReference>? CallChosen;

    public bool CanShow(CallStackNode node) => Log is not null && node.Frame is { Address: not 0, Pdb.Length: > 0 };

    public void SetSource(TimeTravelCallLog? log, IReadOnlyDictionary<ulong, IteratorTarget>? iterators = null)
    {
        Cancel();

        CancelSearch();

        Log = log;
        Iterators = iterators ?? new Dictionary<ulong, IteratorTarget>();

        Shown = null;
        Signature = string.Empty;
        Message = DefaultMessage;
        Clear();
        IsLoading = false;
        CloseValueSearch();
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

        SelectQuietly(call is { } index && index >= 0 && index < table.Rows.Count ? table.Rows[index] : null, chosen: true);

        return Task.CompletedTask;
    }

    public ArgumentCell CellAt(int index, int ordinal)
        => Layout is { } layout && index >= 0 && index < LoggedCalls.Count
            ? ArgumentRowBuilder.CellAt(layout, LoggedCalls[index], ordinal)
            : default;

    public CallStackNode? NodeAt(int index) => index >= 0 && index < LoggedCalls.Count ? Log?.NodeOf(LoggedCalls[index]) : null;

    public IteratorTarget? IteratorOf(ulong value) => Iterators.GetValueOrDefault(value);

    public async Task FindValueAsync(ulong value)
    {
        if (Log is not { } log)
        {
            return;
        }

        CancelSearch();

        var search = new CancellationTokenSource();

        Search = search;

        ValueSearch = $"References To 0x{value:X}";
        ValueGroups = [];
        ValueIdentity = null;
        ValueIterator = null;
        IsSearching = true;

        try
        {
            var uses = await log.FindAsync(value, search.Token);

            var kinds = await ResolveKindsAsync(uses.Where(u => u.Location == "RCX"));

            if (search.IsCancellationRequested)
            {
                return;
            }

            var groups = ValueUseGroup.Build(uses, u => kinds.GetValueOrDefault(u.Address));

            ValueGroups = groups;
            ValueIterator = Iterators.GetValueOrDefault(value);
            ValueIdentity = ValueIterator is null ? Identity(groups) : null;
            ValueSearch = uses.Count == 0 ? $"No References To 0x{value:X}" : $"References To 0x{value:X}";
        }
        catch (OperationCanceledException) when (search.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            logger.LogWarning(exception, "Searching the call log for {Value} failed", value);

            ValueSearch = exception.Message;
        }
        finally
        {
            if (ReferenceEquals(Search, search))
            {
                IsSearching = false;
            }
        }
    }

    public void CloseValueSearch()
    {
        CancelSearch();

        ValueSearch = null;
        ValueGroups = [];
        ValueIdentity = null;
        ValueIterator = null;
        IsSearching = false;
    }

    public void Dispose()
    {
        Cancel();

        CancelSearch();
    }

    private async Task LoadAsync(CallStackNode node, int? select)
    {
        if (Log is not { } log || node.Frame is not { } frame)
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

            var layout = ArgumentLayout.For(parsed);

            Signature = Header(parsed, signature ?? node.Symbol);

            if (log.CallsOf(frame.Address, frame.Instance) is not { Count: > 0 } calls)
            {
                Message = "The function was not called on the query's threads";

                return;
            }

            LoadingMessage = "Reading Call Log";

            var table = await Task.Run(() => ArgumentRowBuilder.Table(layout, calls), request.Token);

            Layout = layout;
            LoggedCalls = calls;
            CallTable = table;

            if (select is { } index && index >= 0 && index < table.Rows.Count)
            {
                SelectQuietly(table.Rows[index], chosen: true);
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

    private async Task<Dictionary<ulong, FunctionKind?>> ResolveKindsAsync(IEnumerable<TimeTravelValueUse> uses)
    {
        var kinds = new Dictionary<ulong, FunctionKind?>();

        foreach (var use in uses)
        {
            if (kinds.ContainsKey(use.Address) || use.First.Node?.Frame is not { } frame)
            {
                continue;
            }

            var decoratedName = await symbols.ResolveFrameDecoratedNameAsync(frame);

            kinds[use.Address] = decoratedName is null ? null : FunctionSignature.KindOf(decoratedName);
        }

        return kinds;
    }

    private static string? Identity(IReadOnlyList<ValueUseGroup> groups)
    {
        if (groups.FirstOrDefault(g => g.Role == TimeTravelValueRole.CalledOn) is not { Rows: [var first, ..] })
        {
            return null;
        }

        return first.Use.First.Node?.Frame?.Resolved?.ClassName is { Length: > 0 } className ? $"Is A {className}" : null;
    }

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
        SelectedCall = null;
        CallTable = null;
    }

    private void Cancel()
    {
        Request?.Cancel();

        Request?.Dispose();

        Request = null;
    }

    private void CancelSearch()
    {
        Search?.Cancel();

        Search?.Dispose();

        Search = null;
    }
}
