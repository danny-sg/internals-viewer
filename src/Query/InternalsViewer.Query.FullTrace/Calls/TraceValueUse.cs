using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Calls.Enums;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed record TraceValueUse(ulong Address,
                                   ulong Instance,
                                   string Location,
                                   int Calls,
                                   TraceValueCall First,
                                   TraceValueCall Last)
{
    public TraceValueRole GetRole(FunctionType? type) => Location switch
    {
        "RCX" when type == FunctionType.Member 
            => TraceValueRole.CalledOn,
        "RAX" 
            => TraceValueRole.Returned,
        _ => TraceValueRole.Passed
    };
}
