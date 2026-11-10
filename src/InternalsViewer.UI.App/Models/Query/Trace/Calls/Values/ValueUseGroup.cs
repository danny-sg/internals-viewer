using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Values;

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
            var calls = Rows.Sum(r => r.Use.CallCount);

            return $"{CountText.GetText(Rows.Count, "Function", "Functions")}, {CountText.GetCalls(calls)}";
        }
    }

    public static ValueUseGroup Create(TraceValueUseGroup group, ulong value = 0, Func<TraceValueUse, CallFrame?>? getFrame = null)
        => new(group.Role, [.. group.Uses.Select(u => new ValueUseRow(u, value, getFrame?.Invoke(u)))]);
}
