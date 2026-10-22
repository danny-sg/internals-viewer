using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Native.Enums;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal sealed class TraceHooks
{
    public TraceHooks(IReadOnlyList<ITraceHook> hooks)
    {
        All = [.. hooks];

        var indexes = new Dictionary<ulong, int>();

        var functions = new List<HookFunction>();

        var registered = new List<ITraceHook[]>();

        foreach (var hook in hooks)
        {
            ITraceHook[] single = [hook];

            foreach (var function in hook.Functions)
            {
                ref var index = ref CollectionsMarshal.GetValueRefOrAddDefault(indexes, function.Address, out var exists);

                if (!exists)
                {
                    index = functions.Count;

                    functions.Add(new HookFunction(function.Address, 0, (uint)index));

                    registered.Add(single);
                }
                else if (Array.IndexOf(registered[index], hook) < 0)
                {
                    registered[index] = [.. registered[index], hook];
                }

                functions[index] = functions[index] with { Events = functions[index].Events | (uint)function.Events };
            }
        }

        Hooks = [.. registered];

        Functions = [.. functions];
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
            FunctionAddress = source->FunctionAddress,
            InstanceAddress = source->InstanceAddress,
            Id = source->Id,
            CallIndex = source->CallIndex,
            Thread = source->Thread,
            CallNodeIndex = source->CallNodeIndex,
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
