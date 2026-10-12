using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class ValueUseGroup(FullTraceValueRole role, IReadOnlyList<ValueUseRow> rows)
{
    public FullTraceValueRole Role { get; } = role;

    public string Title => Role switch
    {
        FullTraceValueRole.CalledOn => "Calls On It",
        FullTraceValueRole.Returned => "Returned It",
        _ => "Passed It"
    };

    public IReadOnlyList<ValueUseRow> Rows { get; } = rows;

    public static IReadOnlyList<ValueUseGroup> Build(IReadOnlyList<FullTraceValueUse> uses,
                                                     Func<FullTraceValueUse, FunctionKind?> kindOf)
        => [.. uses.GroupBy(u => u.RoleFor(kindOf(u)))
                   .OrderBy(g => g.Key)
                   .Select(g => new ValueUseGroup(g.Key, [.. g.Select(u => new ValueUseRow(u))]))];
}
