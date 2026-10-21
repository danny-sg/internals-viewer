using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Native.Enums;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal sealed class TraceHooks
{
    public TraceHooks(IReadOnlyList<ITraceHook> hooks)
    {
        All = [.. hooks];

        var groups = hooks.SelectMany(h => h.Functions.Select(f => (Function: f, Hook: h)))
                          .GroupBy(r => r.Function.Address)
                          .ToArray();

        Hooks = [.. groups.Select(g => g.Select(r => r.Hook).Distinct().ToArray())];

        Functions = [.. groups.Select((g, index) => new HookFunction(g.Key, (uint)GetEvents(g), (uint)index))];
    }

    public HookFunction[] Functions { get; }

    public Exception? Failure { get; private set; }

    private ITraceHook[] All { get; }

    private ITraceHook[][] Hooks { get; }

    private ReplayView Replay { get; } = new();

    public unsafe void OnHook(IntPtr hookEvent, IntPtr view, IntPtr session)
    {
        var source = (HookEvent*)hookEvent;

        if (Failure is not null)
        {
            return;
        }

        var hooks = source->Hook < Hooks.Length ? Hooks[source->Hook] : All;

        var call = new TraceHookCall
        {
            Function = source->Function,
            Instance = source->Instance,
            Id = source->Id,
            Call = source->Call,
            Thread = source->Thread,
            Node = source->Node,
            Arguments = new ReadOnlySpan<ulong>(source->Arguments, HookEvent.ArgumentCount),
            ReturnValue = source->ReturnValue,
            StackPointer = source->StackPointer,
            ReturnAddress = source->ReturnAddress,
            Position = new TracePosition(source->Sequence, source->Steps),
            Instructions = source->Instructions
        };

        Replay.View = view;

        Replay.Session = session;

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
            Replay.View = IntPtr.Zero;

            Replay.Session = IntPtr.Zero;
        }
    }

    private static TraceHookEvents GetEvents(IEnumerable<(TraceHookFunction Function, ITraceHook Hook)> registrations)
        => registrations.Aggregate(TraceHookEvents.None, (events, r) => events | r.Function.Events);

    private void Dispatch(ITraceHook hook, HookEventType type, in TraceHookCall call)
    {
        switch (type)
        {
            case HookEventType.Entry:
                hook.OnEntry(call, Replay);
                break;

            case HookEventType.Return:
                hook.OnReturn(call, Replay);
                break;

            case HookEventType.Abandoned:
                hook.OnAbandoned(call);
                break;

            case HookEventType.FrameEnded:
                hook.OnFrameEnded(call);
                break;
        }
    }
}
