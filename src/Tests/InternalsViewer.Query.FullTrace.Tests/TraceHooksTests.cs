using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Tests;

public class TraceHooksTests
{
    [Fact]
    public void A_Function_Hooked_Twice_Is_Registered_Once_With_Both_Events()
    {
        var entries = new Hook(new TraceHookFunction(0x1000, TraceHookEvents.Entry), new TraceHookFunction(0x2000, TraceHookEvents.Entry));

        var returns = new Hook(new TraceHookFunction(0x2000, TraceHookEvents.Return),
                               new TraceHookFunction(0x3000, TraceHookEvents.Return));

        var hooks = new TraceHooks([entries, returns]);

        Assert.Equal([new HookFunction(0x1000, (uint)TraceHookEvents.Entry, 0),
                      new HookFunction(0x2000, (uint)(TraceHookEvents.Entry | TraceHookEvents.Return), 1),
                      new HookFunction(0x3000, (uint)TraceHookEvents.Return, 2)],
                     hooks.Functions);
    }

    private sealed class Hook(params TraceHookFunction[] functions) : ITraceHook
    {
        public IEnumerable<TraceHookFunction> Functions { get; } = functions;
    }
}
