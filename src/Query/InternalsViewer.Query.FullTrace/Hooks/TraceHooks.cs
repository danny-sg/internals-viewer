using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal sealed class TraceHooks
{
    public TraceHooks(IEnumerable<ITraceHook> hooks)
    {
        Hooks = hooks.SelectMany(h => h.Functions.Select(f => (Function: f, Hook: h)))
                     .GroupBy(h => h.Function)
                     .ToDictionary(g => g.Key, g => g.Select(h => h.Hook).Distinct().ToArray());

        Functions = [.. Hooks.Keys];
    }

    public ulong[] Functions { get; }

    public Exception? Failure { get; private set; }

    private Dictionary<ulong, ITraceHook[]> Hooks { get; }

    private ReplayMemory Memory { get; } = new();

    public unsafe void OnHook(IntPtr hookEvent, IntPtr view)
    {
        var source = (HookEvent*)hookEvent;

        if (Failure is not null || !Hooks.TryGetValue(source->Function, out var hooks))
        {
            return;
        }

        var call = new TraceHookCall
        {
            Function = source->Function,
            Instance = source->Instance,
            Id = source->Id,
            CallCount = source->CallCount,
            Call = source->Call,
            Thread = source->Thread,
            Arguments = new ReadOnlySpan<ulong>(source->Arguments, HookEvent.ArgumentCount),
            ReturnValue = source->ReturnValue,
            Callers = new ReadOnlySpan<ulong>(source->Callers, (int)source->CallerCount)
        };

        Memory.View = view;

        try
        {
            foreach (var hook in hooks)
            {
                Dispatch(hook, source->Type, call);
            }
        }
        catch (Exception exception)
        {
            Failure = exception;
        }
        finally
        {
            Memory.View = IntPtr.Zero;
        }
    }

    private void Dispatch(ITraceHook hook, HookEventType type, in TraceHookCall call)
    {
        switch (type)
        {
            case HookEventType.Entry:
                hook.OnEntry(call, Memory);
                break;

            case HookEventType.Return:
                hook.OnReturn(call, Memory);
                break;

            case HookEventType.Abandoned:
                hook.OnAbandoned(call);
                break;
        }
    }
}
