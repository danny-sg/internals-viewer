using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.TimeTravel;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class ValueUseGroup(TimeTravelValueRole role, IReadOnlyList<ValueUseRow> rows)
{
    public TimeTravelValueRole Role { get; } = role;

    public string Title => Role switch
    {
        TimeTravelValueRole.CalledOn => "Calls On It",
        TimeTravelValueRole.Returned => "Returned It",
        TimeTravelValueRole.Passed => "Passed It",
        _ => "Held It Behind A Pointer"
    };

    public IReadOnlyList<ValueUseRow> Rows { get; } = rows;

    public static IReadOnlyList<ValueUseGroup> Build(IReadOnlyList<TimeTravelValueUse> uses,
                                                     Func<TimeTravelValueUse, FunctionKind?> kindOf)
        => [.. uses.GroupBy(u => u.RoleFor(kindOf(u)))
                   .OrderBy(g => g.Key)
                   .Select(g => new ValueUseGroup(g.Key, [.. g.Select(u => new ValueUseRow(u))]))];
}
