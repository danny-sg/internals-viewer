using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.Extensions;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal static class TraceReplayExtensions
{
    public static ulong GetStackArguments(this ITraceReplay replay, in TraceHookCall call)
    {
        var hasReturnAddress = replay.TryRead(call.StackPointer, out ulong top) && top == call.ReturnAddress;

        return call.StackPointer + (ulong)(hasReturnAddress ? ArgumentLayout.StackArgumentOffset : ArgumentLayout.HomeSpaceSize);
    }
}
