using InternalsViewer.Query.CallStack.Arguments;

namespace InternalsViewer.Query.FullTrace.CallLog;

public sealed record FullTraceValueUse(ulong Address,
                                       ulong Instance,
                                       string Location,
                                       int Calls,
                                       FullTraceValueMatch First,
                                       FullTraceValueMatch Last)
{
    public FullTraceValueRole RoleFor(FunctionKind? kind) => Location switch
    {
        "RCX" when kind == FunctionKind.Member 
            => FullTraceValueRole.CalledOn,
        "RAX" 
            => FullTraceValueRole.Returned,
        _ => FullTraceValueRole.Passed
    };
}
