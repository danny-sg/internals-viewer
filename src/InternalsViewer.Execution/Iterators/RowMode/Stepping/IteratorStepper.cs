#pragma warning disable VSTHRD003

using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.Interfaces;

namespace InternalsViewer.Execution.Iterators.RowMode.Stepping;

public sealed class IteratorStepper : IAsyncDisposable
{
    private readonly IteratorDefinition _definition;

    private readonly IteratorContext _context;

    private readonly SemaphoreSlim _delivered = new(0);

    private readonly SemaphoreSlim _resume = new(0);

    private readonly SemaphoreSlim _continue = new(0);

    private readonly CancellationTokenSource _engineCancellation = new();

    private readonly TaskCompletionSource _opened = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly List<AccessStep> _history = [];

    private readonly Dictionary<int, AccessCounters> _countersByNode = [];

    private Task? _engine;

    private AccessStep? _pending;

    private bool _isRequestOutstanding;

    private bool _hasDelivered;

    private bool _hasPendingStep;

    private volatile bool _freeRun;

    private Func<AccessStep, bool>? _stopAfter;

    private TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IteratorStepper(IIterator root, IteratorDefinition definition, IteratorContext context)
    {
        Root = root;

        _definition = definition;
        _context = context with { Steps = new GateSink(this) };
    }

    public IIterator Root { get; }

    public IReadOnlyList<AccessStep> History => _history;

    public AccessStep? Current => _history.Count == 0 ? null : _history[^1];

    public bool IsComplete { get; private set; }

    public AccessCounters Counters { get; private set; }

    public AccessCounters CountersFor(int nodeId) => _countersByNode.GetValueOrDefault(nodeId);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        EnsureEngine();

        var completed = await Task.WhenAny(_started.Task, _opened.Task, _engine!).WaitAsync(cancellationToken);

        await completed;
    }

    public async Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken)
    {
        if (IsComplete)
        {
            return null;
        }

        EnsureEngine();

        if (!_isRequestOutstanding)
        {
            _isRequestOutstanding = true;

            if (_hasDelivered)
            {
                _continue.Release();
            }

            _resume.Release();
        }

        await _delivered.WaitAsync(cancellationToken);

        if (!_hasPendingStep)
        {
            IsComplete = true;

            await _engine!;

            return null;
        }

        _hasPendingStep = false;

        _isRequestOutstanding = false;

        _hasDelivered = true;

        return _pending;
    }

    /// <summary>
    /// Runs the engine to completion or until a step satisfies the stop condition without parking between steps
    /// </summary>
    public async Task DrainAsync(Func<AccessStep, bool>? stopAfter, CancellationToken cancellationToken)
    {
        if (IsComplete)
        {
            return;
        }

        _stopAfter = stopAfter;

        _drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var alreadyRunning = _engine is not null;

        _freeRun = true;

        EnsureEngine();

        if (_hasDelivered)
        {
            _hasDelivered = false;

            _continue.Release();
        }
        else if (alreadyRunning && !_engine!.IsCompleted)
        {
            _resume.Release();
        }

        try
        {
            var finished = await Task.WhenAny(_drained.Task, _engine!).WaitAsync(cancellationToken);

            if (finished == _engine)
            {
                IsComplete = true;

                await _engine;
            }
        }
        catch (OperationCanceledException)
        {
            _freeRun = false;

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _engineCancellation.CancelAsync();

        if (_engine is not null)
        {
            try
            {
                await _engine;
            }
            catch
            {
                // No-op
            }
        }

        _engineCancellation.Dispose();
        _delivered.Dispose();
        _resume.Dispose();
        _continue.Dispose();
    }

    private void EnsureEngine()
    {
        if (_engine is not null)
        {
            return;
        }

        _engine = Task.Run(RunAsync);

        _ = _engine.ContinueWith(static (_, state) => ((IteratorStepper)state!)._delivered.Release(),
                                 this,
                                 CancellationToken.None,
                                 TaskContinuationOptions.ExecuteSynchronously,
                                 TaskScheduler.Default);
    }

    private async Task RunAsync()
    {
        var cancellationToken = _engineCancellation.Token;

        await Root.OpenAsync(_definition, _context, cancellationToken);

        _opened.TrySetResult();

        while (await Root.GetRowAsync(cancellationToken) is not null)
        {
        }

        await Root.CloseAsync();
    }

    private AccessStep Record(AccessStep step)
    {
        if (step.Counters != default)
        {
            var previous = _countersByNode.GetValueOrDefault(step.NodeId);

            _countersByNode[step.NodeId] = step.Counters;

            Counters = Counters.Add(step.Counters.Subtract(previous));
        }

        var stamped = step with { Counters = Counters };

        _history.Add(stamped);

        return stamped;
    }

    private async Task StopFreeRunAsync(AccessStep step)
    {
        _freeRun = false;

        _pending = step;

        _hasPendingStep = true;

        _hasDelivered = true;

        _drained.TrySetResult();

        await _continue.WaitAsync(_engineCancellation.Token);
    }

    private sealed class GateSink(IteratorStepper owner) : IStepSink
    {
        public async ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken)
        {
            owner._started.TrySetResult();

            if (owner._freeRun)
            {
                var recorded = owner.Record(step);

                if (owner._stopAfter?.Invoke(recorded) != true)
                {
                    return;
                }

                await owner.StopFreeRunAsync(recorded);

                return;
            }

            await owner._resume.WaitAsync(owner._engineCancellation.Token);

            if (owner._freeRun)
            {
                var recorded = owner.Record(step);

                if (owner._stopAfter?.Invoke(recorded) != true)
                {
                    return;
                }

                await owner.StopFreeRunAsync(recorded);

                return;
            }

            owner._pending = owner.Record(step);

            owner._hasPendingStep = true;

            owner._delivered.Release();

            await owner._continue.WaitAsync(owner._engineCancellation.Token);
        }
    }
}
