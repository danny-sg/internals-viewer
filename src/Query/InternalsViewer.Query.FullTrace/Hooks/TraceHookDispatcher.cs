using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Native.Enums;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal sealed class TraceHookDispatcher
{
    private const uint AllHookMask = uint.MaxValue;

    public TraceHookDispatcher(IReadOnlyList<ITraceHook> hooks, EngineLayout layout)
    {
        All = [.. hooks];

        Replay = new ReplayView { Layout = layout };

        var capacity = 0;

        foreach (var hook in hooks)
        {
            capacity += hook.Functions.Count();
        }

        var indexes = new Dictionary<ulong, int>(capacity);

        var functions = new List<HookFunction>(capacity);

        for (var hook = 0; hook < All.Length; hook++)
        {
            var bit = 1u << hook;

            foreach (var function in All[hook].Functions)
            {
                ref var index = ref CollectionsMarshal.GetValueRefOrAddDefault(indexes, function.Address, out var exists);

                if (!exists)
                {
                    index = functions.Count;

                    functions.Add(new HookFunction(function.Address, 0, 0));
                }

                functions[index] = functions[index] with
                {
                    EventMask = functions[index].EventMask | (uint)function.Events,
                    HookMask = functions[index].HookMask | bit
                };
            }
        }

        Functions = [.. functions];
    }

    public HookFunction[] Functions { get; }

    public Exception? Failure { get; private set; }

    private ITraceHook[] All { get; }

    private ReplayView Replay { get; }

    public unsafe void OnHook(IntPtr hookEvent, IntPtr view, IntPtr session)
    {
        var source = (HookEvent*)hookEvent;

        if (Failure is not null)
        {
            return;
        }

        var hookMask = source->HookMask;

        var call = new TraceHookCall
        {
            FunctionAddress = source->FunctionAddress,
            InstanceAddress = source->InstanceAddress,
            CallId = source->CallId,
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
            for (var hook = 0; hook < All.Length; hook++)
            {
                if (hookMask == AllHookMask || (hookMask & (1u << hook)) != 0)
                {
                    Dispatch(All[hook], source->Type, call);
                }
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