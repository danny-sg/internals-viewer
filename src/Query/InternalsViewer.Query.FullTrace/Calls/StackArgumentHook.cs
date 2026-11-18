using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;

namespace InternalsViewer.Query.FullTrace.Calls;

internal sealed class StackArgumentHook(AddressTable<int> functions, TraceCallStore.Builder store) : ITraceHook
{
    public IEnumerable<TraceHookFunction> Functions => functions.Keys.Select(f => new TraceHookFunction(f, TraceHookEvents.Entry));

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!functions.TryGetValue(call.FunctionAddress, out var count))
        {
            return;
        }

        var stack = replay.GetStackArguments(call);

        store.AddStackArguments(call.FunctionAddress, call.InstanceAddress, call.CallIndex, stack[..Math.Min(count, stack.Length)]);
    }
}
