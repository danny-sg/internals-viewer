using InternalsViewer.Query.CallStack.Arguments;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record TimeTravelValueUse(ulong Address,
                                        ulong Instance,
                                        string Location,
                                        int Calls,
                                        TimeTravelValueMatch First,
                                        TimeTravelValueMatch Last)
{
    public TimeTravelValueRole RoleFor(FunctionKind? kind) => Location switch
    {
        "RCX" when kind == FunctionKind.Member => TimeTravelValueRole.CalledOn,
        "RAX" => TimeTravelValueRole.Returned,
        ['*', ..] => TimeTravelValueRole.BehindPointer,
        _ => TimeTravelValueRole.Passed
    };
}
