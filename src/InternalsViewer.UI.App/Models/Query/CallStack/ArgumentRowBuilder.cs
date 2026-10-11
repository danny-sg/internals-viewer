using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;
using InternalsViewer.Query.Results;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public static class ArgumentRowBuilder
{
    private const string UnknownType = "Unknown";

    private const string DidNotReturn = "Did Not Return";

    private const int FixedColumns = 2;

    private const int IndexWidth = 70;

    private const int ThreadWidth = 80;

    private static readonly Color ReturnTint = Color.FromArgb(0x12, 0x4C, 0xA3, 0xE0);

    public static QueryResultSet Table(ArgumentLayout layout, IReadOnlyList<TimeTravelArgumentCall> calls)
    {
        var slots = CapturedSlots(layout);

        var returnType = layout.ReturnType ?? UnknownType;

        var hasReturn = HasReturn(returnType);

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

    public static ArgumentCell CellAt(ArgumentLayout layout, TimeTravelArgumentCall call, int ordinal)
    {
        var slots = CapturedSlots(layout);

        var position = ordinal - FixedColumns;

        if (position >= 0 && position < slots.Count)
        {
            return new ArgumentCell(call.Value(slots[position]), call.LocatorAt(slots[position].Index));
        }

        if (position == slots.Count && HasReturn(layout.ReturnType ?? UnknownType) && call.Returned)
        {
            return new ArgumentCell(call.ReturnValue, call.LocatorAt(LocatorReads.ReturnRegister));
        }

        return default;
    }

    private static List<ArgumentSlot> CapturedSlots(ArgumentLayout layout) => [.. layout.Slots.Where(TimeTravelArgumentCall.IsCaptured)];

    private static bool HasReturn(string returnType) => returnType != "void" && TimeTravelArgumentCall.IsReturnCaptured(returnType);

    private static object?[] Values(List<ArgumentSlot> slots, string? returnType, TimeTravelArgumentCall call, int index)
    {
        var values = new List<object?> { (long)index + 1, call.ThreadId };

        foreach (var slot in slots)
        {
            values.Add(call.LocatorAt(slot.Index) is { } locator ? Address(locator) : Text(slot.Type, call.Value(slot)));
        }

        if (returnType is not null)
        {
            values.Add(!call.Returned
                           ? DidNotReturn
                           : call.LocatorAt(LocatorReads.ReturnRegister) is { } locator
                               ? Address(locator)
                               : ArgumentValue.Format(returnType, call.ReturnValue));
        }

        return [.. values];
    }

    private static object Address(TimeTravelLocator locator) => locator.Kind switch
    {
        LocatorKind.Row => locator.Row,
        LocatorKind.ColumnStoreObject => locator.ToString(),
        _ => locator.Page
    };

    private static ResultColumn Column(int ordinal, string name, string type, string detail, Color? tint = null)
        => new(ordinal, name, typeof(string), false) { TypeName = type, Detail = detail, BackgroundColour = tint };

    private static string Text(string type, ulong? value)
        => value is { } raw ? ArgumentValue.Format(type, raw) : "Unreadable";

    private static string ReturnSource(string type) => ArgumentValue.IsFloating(type) ? "XMM0" : "RAX";
}
