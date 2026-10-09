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
    private string _function = string.Empty;

    [ObservableProperty]
    private string? _summary;

    [ObservableProperty]
    private string? _message = DefaultMessage;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingMessage = "Loading...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCall))]
    private IReadOnlyList<ArgumentRow> _rows = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCalls))]
    private QueryResultSet? _callTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedCall;

    [ObservableProperty]
    private string? _callTitle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCallNode))]
    private CallStackNode? _callNode;

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

    public bool HasCall => Rows.Count > 0;

    public bool HasCalls => CallTable is not null;

    public bool HasCallNode => CallNode is not null;

    public bool HasValueSearch => ValueSearch is not null;

    public string? ValueIteratorLabel => ValueIterator is { } iterator ? $"Is The {iterator.Label} Iterator" : null;

    public CallStackNode? Shown { get; private set; }

    public int? SelectedCallIndex => SelectedCall is { Id: var index } ? (int)index : null;

    private TimeTravelCallLog? Log { get; set; }

    private IReadOnlyDictionary<ulong, IteratorTarget> Iterators { get; set; } = new Dictionary<ulong, IteratorTarget>();

    private ArgumentLayout? Layout { get; set; }

    private IReadOnlyList<TimeTravelArgumentCall> LoggedCalls { get; set; } = [];

    private CancellationTokenSource? Request { get; set; }

    private CancellationTokenSource? Search { get; set; }

    public bool CanShow(CallStackNode node) => Log is not null && node.Frame is { Address: not 0, Pdb.Length: > 0 };

    public void SetSource(TimeTravelCallLog? log, IReadOnlyDictionary<ulong, IteratorTarget>? iterators = null)
    {
        Cancel();

        CancelSearch();

        Log = log;
        Iterators = iterators ?? new Dictionary<ulong, IteratorTarget>();

        Shown = null;
        Function = string.Empty;
        Summary = null;
        Message = DefaultMessage;
        Clear();
        IsLoading = false;
        CloseValueSearch();
    }

    public Task ShowAsync(CallStackNode node)
        => ReferenceEquals(Shown, node) && (IsLoading || CallTable is not null) ? Task.CompletedTask : LoadAsync(node, null);

    public Task ShowAsync(CallStackNode node, int? call)
    {
        if (!ReferenceEquals(Shown, node) || IsLoading || CallTable is not { } table)
        {
            return LoadAsync(node, call);
        }

        SelectedCall = call is { } index && index >= 0 && index < table.Rows.Count ? table.Rows[index] : null;

        return Task.CompletedTask;
    }


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
        Function = node.Symbol;
        Summary = null;
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

            if (parsed.Kind == FunctionKind.Static)
            {
                Function = $"static {node.Symbol}";
            }

            if (log.CallsOf(frame.Address, frame.Instance) is not { Count: > 0 } calls)
            {
                Message = "The function was not called on the query's threads";

                return;
            }

            LoadingMessage = "Reading Call Log";

            var table = await Task.Run(() => ArgumentRowBuilder.Table(layout, calls), request.Token);

            Summary = Describe(calls.Count, frame.Instance);
            Layout = layout;
            LoggedCalls = calls;
            CallTable = table;

            if (select is { } index && index >= 0 && index < table.Rows.Count)
            {
                SelectedCall = table.Rows[index];
            }
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            logger.LogWarning(exception, "Reading the arguments of {Function} failed", Function);

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

    private static string Describe(int calls, ulong instance)
    {
        var summary = calls == 1 ? "1 Call" : $"{calls:N0} Calls";

        if (instance != 0)
        {
            summary += $" on This Instance (0x{instance:X})";
        }

        return summary;
    }

    partial void OnSelectedCallChanged(ResultRow<long>? value)
    {
        if (value is { Id: var index } && Layout is { } layout && index >= 0 && index < LoggedCalls.Count)
        {
            var call = LoggedCalls[(int)index];

            Rows = ArgumentRowBuilder.ForCall(layout, call, Iterators);
            CallTitle = $"Call {index + 1:N0} on Thread {call.ThreadId}";
            CallNode = Log?.NodeOf(call);
            SelectedCallReference = CallNode is { } node ? new CallReference(node, (int)index) : null;

            return;
        }

        Rows = [];
        CallTitle = null;
        CallNode = null;
        SelectedCallReference = null;
    }

    private void Clear()
    {
        Layout = null;
        LoggedCalls = [];
        SelectedCall = null;
        Rows = [];
        CallTitle = null;
        CallNode = null;
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
