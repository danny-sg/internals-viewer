using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Symbols.Enums;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed record TraceValueUse(ulong FunctionAddress,
                                   ulong InstanceAddress,
                                   string Location,
                                   int CallCount,
                                   TraceValueCall First,
                                   TraceValueCall Last)
{
    public TraceValueRole GetRole(FunctionType? type) => Location switch
    {
        ArgumentLayout.ThisRegisterName when type == FunctionType.Member 
            => TraceValueRole.CalledOn,
        ArgumentLayout.ReturnRegisterName
            => TraceValueRole.Returned,
        _ => TraceValueRole.Passed
    };
}
