using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class ValueUseGroup(TraceValueRole role, IReadOnlyList<ValueUseRow> rows)
{
    public TraceValueRole Role { get; } = role;

    public string Title => Role switch
    {
        TraceValueRole.CalledOn => "Calls On It",
        TraceValueRole.Returned => "Returned It",
        _ => "Passed It"
    };

    public IReadOnlyList<ValueUseRow> Rows { get; } = rows;

    public static IReadOnlyList<ValueUseGroup> Build(IReadOnlyList<TraceValueUse> uses,
                                                     Func<TraceValueUse, FunctionKind?> kindOf)
        => [.. uses.GroupBy(u => u.RoleFor(kindOf(u)))
                   .OrderBy(g => g.Key)
                   .Select(g => new ValueUseGroup(g.Key, [.. g.Select(u => new ValueUseRow(u))]))];
}
