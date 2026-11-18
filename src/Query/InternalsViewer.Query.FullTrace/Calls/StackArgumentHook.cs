using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Replay.Hooks;
using InternalsViewer.Query.FullTrace.Replay.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Replay.Hooks.Interfaces;
using InternalsViewer.Query.FullTrace.Replay;

namespace InternalsViewer.Query.FullTrace.Calls;

internal sealed class StackArgumentHook(AddressTable<int> functions, ReplayCallLog log) : ITraceHook
{
    public IEnumerable<TraceHookFunction> Functions => functions.Keys.Select(f => new TraceHookFunction(f, TraceHookEvents.Entry));

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!functions.TryGetValue(call.FunctionAddress, out var count))
        {
            return;
        }

        var stack = replay.GetStackArguments(call);

        log.AddStackArguments(call.FunctionAddress, call.InstanceAddress, call.CallIndex, stack[..Math.Min(count, stack.Length)]);
    }
}
