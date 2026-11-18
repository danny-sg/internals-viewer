using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Replay.Hooks;
using InternalsViewer.Query.FullTrace.Replay.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Replay.Hooks.Interfaces;
using InternalsViewer.Query.FullTrace.Replay.Native;

namespace InternalsViewer.Query.FullTrace.Tests.Replay.Hooks;

public class TraceHookDispatcherTests
{
    [Fact]
    public void A_Function_Hooked_Twice_Is_Registered_Once_With_Both_Events()
    {
        var entries = new Hook(new TraceHookFunction(0x1000, TraceHookEvents.Entry), new TraceHookFunction(0x2000, TraceHookEvents.Entry));

        var returns = new Hook(new TraceHookFunction(0x2000, TraceHookEvents.Return),
                               new TraceHookFunction(0x3000, TraceHookEvents.Return));

        var hooks = new TraceHookDispatcher([entries, returns], EngineLayout.Latest);

        Assert.Equal([new HookFunction(0x1000, (uint)TraceHookEvents.Entry, 1),
                      new HookFunction(0x2000, (uint)(TraceHookEvents.Entry | TraceHookEvents.Return), 3),
                      new HookFunction(0x3000, (uint)TraceHookEvents.Return, 2)],
                     hooks.Functions);
    }

    private sealed class Hook(params TraceHookFunction[] functions) : ITraceHook
    {
        public IEnumerable<TraceHookFunction> Functions { get; } = functions;
    }
}
