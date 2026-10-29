using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;

namespace InternalsViewer.UI.App.Models.Query.Trace.CallStack.Values;

public sealed class ValueUseGroup(TraceValueRole role, IReadOnlyList<ValueUseRow> rows)
{
    public TraceValueRole Role { get; } = role;

    public string Title => Role switch
    {
        TraceValueRole.CalledOn => "Called by",
        TraceValueRole.Returned => "Returned by",
        _ => "Referenced by"
    };

    public IReadOnlyList<ValueUseRow> Rows { get; } = rows;

    public string CountLabel
    {
        get
        {
            var calls = Rows.Sum(r => r.Use.Calls);

            return $"{Rows.Count:N0} {(Rows.Count == 1 ? "Function" : "Functions")}, {calls:N0} {(calls == 1 ? "Call" : "Calls")}";
        }
    }

    public static ValueUseGroup Create(TraceValueUseGroup group, ulong value = 0, Func<TraceValueUse, CallstackFrame?>? getFrame = null)
        => new(group.Role, [.. group.Uses.Select(u => new ValueUseRow(u, value, getFrame?.Invoke(u)))]);
}
