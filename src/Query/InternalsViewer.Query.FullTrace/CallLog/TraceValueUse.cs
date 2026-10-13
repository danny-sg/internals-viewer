using InternalsViewer.Query.CallStack.Arguments;

namespace InternalsViewer.Query.FullTrace.CallLog;

public sealed record TraceValueUse(ulong Address,
                                   ulong Instance,
                                   string Location,
                                   int Calls,
                                   TraceValueMatch First,
                                   TraceValueMatch Last)
{
    public TraceValueRole RoleFor(FunctionKind? kind) => Location switch
    {
        "RCX" when kind == FunctionKind.Member 
            => TraceValueRole.CalledOn,
        "RAX" 
            => TraceValueRole.Returned,
        _ => TraceValueRole.Passed
    };
}
