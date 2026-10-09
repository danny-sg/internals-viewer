using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.Results;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public static class ArgumentRowBuilder
{
    private const string UnknownType = "Unknown";

    private const string NotCaptured = "Not Captured";

    private const string DidNotReturn = "Did Not Return";

    private const int IndexWidth = 70;

    private const int ThreadWidth = 80;

    private static readonly Color ReturnTint = Color.FromArgb(0x12, 0x4C, 0xA3, 0xE0);

    public static IReadOnlyList<ArgumentRow> ForCall(ArgumentLayout layout,
                                                     TimeTravelArgumentCall call,
                                                     IReadOnlyDictionary<ulong, IteratorTarget>? iterators = null)
    {
        var rows = new List<ArgumentRow>();

        foreach (var slot in layout.Slots)
        {
            if (!TimeTravelArgumentCall.IsCaptured(slot))
            {
                rows.Add(new ArgumentRow(slot.Name, slot.Type, slot.Source, NotCaptured));

                continue;
            }

            var value = call.Value(slot);

            rows.Add(new ArgumentRow(slot.Name,
                                     slot.Type,
                                     slot.Source,
                                     Text(slot.Type, value),
                                     value,
                                     IteratorOf(value, iterators)));
        }

        var returnType = layout.ReturnType ?? UnknownType;

        if (returnType == "void")
        {
            return rows;
        }

        if (!TimeTravelArgumentCall.IsReturnCaptured(returnType))
        {
            rows.Add(new ArgumentRow("Return", returnType, ReturnSource(returnType), NotCaptured));

            return rows;
        }

        var returned = call.Returned ? call.ReturnValue : (ulong?)null;

        rows.Add(new ArgumentRow("Return",
                                 returnType,
                                 ReturnSource(returnType),
                                 call.Returned ? ArgumentValue.Format(returnType, call.ReturnValue) : DidNotReturn,
                                 returned,
                                 IteratorOf(returned, iterators)));

        return rows;
    }

    public static QueryResultSet Table(ArgumentLayout layout, IReadOnlyList<TimeTravelArgumentCall> calls)
    {
        var slots = layout.Slots.Where(TimeTravelArgumentCall.IsCaptured).ToList();

        var returnType = layout.ReturnType ?? UnknownType;

        var hasReturn = returnType != "void" && TimeTravelArgumentCall.IsReturnCaptured(returnType);

        var columns = new List<ResultColumn>
        {
            new(0, "Call", typeof(long), false) { Width = IndexWidth, Alignment = ResultAlignment.Right },
            new(1, "Thread", typeof(uint), false) { Width = ThreadWidth, Alignment = ResultAlignment.Right }
        };

        foreach (var slot in slots)
        {
            columns.Add(Column(columns.Count, slot.Name, slot.Type, slot.Source));
        }

        if (hasReturn)
        {
            columns.Add(Column(columns.Count, "Return", returnType, ReturnSource(returnType), ReturnTint));
        }

        var source = new ArgumentCallSource(calls,
                                            columns.Count,
                                            (call, index) => Values(slots, hasReturn ? returnType : null, call, index));

        var rows = new ResultRow<long>[calls.Count];

        for (var index = 0; index < rows.Length; index++)
        {
            rows[index] = new ResultRow<long>(source, index) { Id = index };
        }

        return new QueryResultSet { Columns = columns, Rows = rows };
    }

    private static object?[] Values(List<ArgumentSlot> slots, string? returnType, TimeTravelArgumentCall call, int index)
    {
        var values = new List<object?> { (long)index + 1, call.ThreadId };

        foreach (var slot in slots)
        {
            values.Add(Text(slot.Type, call.Value(slot)));
        }

        if (returnType is not null)
        {
            values.Add(call.Returned ? ArgumentValue.Format(returnType, call.ReturnValue) : DidNotReturn);
        }

        return [.. values];
    }

    private static IteratorTarget? IteratorOf(ulong? value, IReadOnlyDictionary<ulong, IteratorTarget>? iterators)
    {
        if (value is not { } address || iterators is null)
        {
            return null;
        }

        return iterators.TryGetValue(address, out var target) ? target : null;
    }

    private static ResultColumn Column(int ordinal, string name, string type, string detail, Color? tint = null)
        => new(ordinal, name, typeof(string), false) { TypeName = type, Detail = detail, BackgroundColour = tint };

    private static string Text(string type, ulong? value)
        => value is { } raw ? ArgumentValue.Format(type, raw) : "Unreadable";

    private static string ReturnSource(string type) => ArgumentValue.IsFloating(type) ? "XMM0" : "RAX";
}
