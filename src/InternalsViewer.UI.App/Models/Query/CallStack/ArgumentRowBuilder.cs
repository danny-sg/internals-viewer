using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.TimeTravel;
using InternalsViewer.Query.Results;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public static class ArgumentRowBuilder
{
    private const string UnknownType = "Unknown";

    private const string OnEntry = "Entry";

    private const string OnReturn = "Return";

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
            if (slot.Location == ArgumentLocation.NotCaptured)
            {
                rows.Add(new ArgumentRow(slot.Name, slot.Type, slot.Source, "Not Captured"));

                continue;
            }

            var value = call.Value(slot);

            rows.Add(new ArgumentRow(slot.Name,
                                     slot.Type,
                                     slot.Source,
                                     Text(slot.Type, value),
                                     Searchable(slot.Type, value),
                                     IteratorOf(value, iterators)));

            if (!slot.IsPrimitivePointer || ArgumentValue.Pointee(slot.Type) is not { } pointee)
            {
                continue;
            }

            rows.Add(new ArgumentRow($"*{slot.Name}", pointee, OnEntry, PointeeText(call, slot, pointee, onReturn: false)));

            rows.Add(new ArgumentRow($"*{slot.Name}",
                                     pointee,
                                     OnReturn,
                                     call.Returned ? PointeeText(call, slot, pointee, onReturn: true) : DidNotReturn));
        }

        var returnType = layout.ReturnType ?? UnknownType;

        if (returnType != "void")
        {
            var returned = call.Returned && !ArgumentValue.IsFloating(returnType) ? call.ReturnValue : (ulong?)null;

            rows.Add(new ArgumentRow("Return",
                                     returnType,
                                     ReturnSource(returnType),
                                     call.Returned ? ReturnText(returnType, call) : DidNotReturn,
                                     returned,
                                     IteratorOf(returned, iterators)));
        }

        return rows;
    }

    public static QueryResultSet Table(ArgumentLayout layout, IReadOnlyList<TimeTravelArgumentCall> calls)
    {
        var slots = layout.Slots.Where(s => s.Location != ArgumentLocation.NotCaptured).ToList();

        var returnType = layout.ReturnType ?? UnknownType;

        var columns = new List<ResultColumn>
        {
            new(0, "#", typeof(long), false) { Width = IndexWidth, Alignment = ResultAlignment.Right },
            new(1, "Thread", typeof(uint), false) { Width = ThreadWidth, Alignment = ResultAlignment.Right }
        };

        foreach (var slot in slots)
        {
            columns.Add(Column(columns.Count, slot.Name, slot.Type, slot.Source));

            if (slot.IsPrimitivePointer && ArgumentValue.Pointee(slot.Type) is { } pointee)
            {
                columns.Add(Column(columns.Count, $"*{slot.Name}", pointee, OnEntry));
                columns.Add(Column(columns.Count, $"*{slot.Name}", pointee, OnReturn, ReturnTint));
            }
        }

        if (returnType != "void")
        {
            columns.Add(Column(columns.Count, "Return", returnType, ReturnSource(returnType), ReturnTint));
        }

        var source = new ArgumentCallSource(calls, columns.Count, (call, index) => Values(slots, returnType, call, index));

        var rows = new ResultRow<long>[calls.Count];

        for (var index = 0; index < rows.Length; index++)
        {
            rows[index] = new ResultRow<long>(source, index) { Id = index };
        }

        return new QueryResultSet { Columns = columns, Rows = rows };
    }

    private static object?[] Values(List<ArgumentSlot> slots, string returnType, TimeTravelArgumentCall call, int index)
    {
        var values = new List<object?> { (long)index + 1, call.ThreadId };

        foreach (var slot in slots)
        {
            values.Add(Text(slot.Type, call.Value(slot)));

            if (slot.IsPrimitivePointer && ArgumentValue.Pointee(slot.Type) is { } pointee)
            {
                values.Add(PointeeText(call, slot, pointee, onReturn: false));
                values.Add(call.Returned ? PointeeText(call, slot, pointee, onReturn: true) : DidNotReturn);
            }
        }

        if (returnType != "void")
        {
            values.Add(call.Returned ? ReturnText(returnType, call) : DidNotReturn);
        }

        return [.. values];
    }

    private static ulong? Searchable(string type, ulong? value) => ArgumentValue.IsFloating(type) ? null : value;

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

    private static string PointeeText(TimeTravelArgumentCall call, ArgumentSlot slot, string pointee, bool onReturn)
    {
        if (call.Value(slot) is null or 0)
        {
            return "Null Pointer";
        }

        return call.Pointee(slot, onReturn) is { } value ? ArgumentValue.Format(pointee, value) : "Unreadable";
    }

    private static string ReturnSource(string type) => ArgumentValue.IsFloating(type) ? "XMM0" : "RAX";

    private static string ReturnText(string type, TimeTravelArgumentCall call)
        => ArgumentValue.Format(type, ArgumentValue.IsFloating(type) ? call.FloatingReturnValue : call.ReturnValue);
}
