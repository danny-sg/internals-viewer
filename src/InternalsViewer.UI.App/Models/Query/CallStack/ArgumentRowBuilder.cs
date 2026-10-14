using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.Results;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public static class ArgumentRowBuilder
{
    private const string UnknownType = "Unknown";

    private const string DidNotReturn = "Did Not Return";

    private const string NotCaptured = "Not Captured";

    private const int FixedColumns = 2;

    private const int IndexWidth = 70;

    private const int ThreadWidth = 80;

    private const ulong SmallestAddress = 0x10000;

    private const string ThisType = "this";

    private const string ReturnArrow = "  \u2192  ";

    private const string ReturnName = "Return";

    private static readonly Color ReturnTint = Color.FromArgb(0x12, 0x4C, 0xA3, 0xE0);

    private static readonly string[] Registers = ["RCX", "RDX", "R8", "R9"];

    public static QueryResultSet Table(ArgumentLayout layout,
                                       IReadOnlyList<TraceArgumentCall> calls,
                                       ValueDescriber? describe = null,
                                       IReadOnlyList<int>? included = null,
                                       bool decode = true)
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
            columns.Add(Column(columns.Count, ReturnName, returnType, ReturnSource(returnType), ReturnTint));
        }

        var source = new ArgumentCallSource(calls,
                                            columns.Count,
                                            (call, index) => Values(slots, hasReturn ? returnType : null, call, index, describe, decode));

        var rows = new ResultRow<long>[included?.Count ?? calls.Count];

        for (var index = 0; index < rows.Length; index++)
        {
            var call = included?[index] ?? index;

            rows[index] = new ResultRow<long>(source, call) { Id = call };
        }

        return new QueryResultSet { Columns = columns, Rows = rows };
    }

    public static ArgumentSlot? SlotAt(ArgumentLayout layout, int ordinal)
    {
        var slots = CapturedSlots(layout);

        var position = ordinal - FixedColumns;

        return position >= 0 && position < slots.Count ? slots[position] : null;
    }

    public static ArgumentCell CellAt(ArgumentLayout layout, TraceArgumentCall call, int ordinal)
    {
        var slots = CapturedSlots(layout);

        var position = ordinal - FixedColumns;

        if (position >= 0 && position < slots.Count)
        {
            return new ArgumentCell(call.Value(slots[position]), AddressAt(call, slots[position].Index), slots[position].Type);
        }

        if (position == slots.Count && layout.ReturnType is { } returnType && HasReturn(returnType) && call.Returned)
        {
            return new ArgumentCell(call.ReturnValue, AddressAt(call, LocatorReadResolver.ReturnRegister), returnType);
        }

        return default;
    }

    public static string? DecodedTextAt(ArgumentLayout layout, TraceArgumentCall call, int ordinal, ValueDescriber? describe)
    {
        if (SlotAt(layout, ordinal) is { } slot)
        {
            return call.Value(slot) is { } value ? Cell(slot, value, call, describe, decode: true).ToString() : null;
        }

        return CellAt(layout, call, ordinal) is { Value: { } returned, Type: { } type }
            ? Cell(ReturnSlot(type), returned, call, describe, decode: true).ToString()
            : null;
    }

    public static IReadOnlyList<CallSignaturePart> Signature(string function,
                                                             ArgumentLayout? layout,
                                                             TraceArgumentCall call,
                                                             ValueDescriber? describe = null)
    {
        var parts = new List<CallSignaturePart>
        {
            new(function, CallSignaturePartKind.Function),
            new("(", CallSignaturePartKind.Punctuation)
        };

        if (layout is null)
        {
            for (var index = 0; index < Registers.Length; index++)
            {
                AddSeparator(parts, index);

                parts.Add(RawPart(call.Slot(index), Registers[index]));
            }

            parts.Add(new CallSignaturePart(")", CallSignaturePartKind.Punctuation));

            if (call.Returned)
            {
                parts.Add(new CallSignaturePart(ReturnArrow, CallSignaturePartKind.Punctuation));

                parts.Add(RawPart(call.ReturnValue, "RAX"));
            }

            return parts;
        }

        for (var index = 0; index < layout.Slots.Count; index++)
        {
            var slot = layout.Slots[index];

            AddSeparator(parts, index);

            if (slot.Name.Length > 0)
            {
                parts.Add(new CallSignaturePart($"{slot.Name} ", CallSignaturePartKind.Name));
            }

            parts.Add(call.Value(slot) is { } value
                          ? ValuePart(slot, value, call, describe)
                          : new CallSignaturePart(slot.Type, CallSignaturePartKind.NotCaptured, toolTip: $"Not Captured ({slot.Source})"));
        }

        parts.Add(new CallSignaturePart(")", CallSignaturePartKind.Punctuation));

        if (layout.ReturnType is { } returnType && HasReturn(returnType) && call.Returned)
        {
            parts.Add(new CallSignaturePart(ReturnArrow, CallSignaturePartKind.Punctuation));

            parts.Add(ValuePart(ReturnSlot(returnType), call.ReturnValue, call, describe));
        }

        return parts;
    }

    public static bool CanFind(string type, ulong value)
        => value != 0 && (type == ThisType || ArgumentValue.IsPointer(type) || value >= SmallestAddress);

    private static CallSignaturePart ValuePart(ArgumentSlot slot,
                                               ulong value,
                                               TraceArgumentCall call,
                                               ValueDescriber? describe)
    {
        var (reading, className) = GetDecoded(slot, value, call, describe);

        return new CallSignaturePart($"0x{value:X}",
                                     CallSignaturePartKind.Value,
                                     value,
                                     $"{slot.Type} ({slot.Source})",
                                     CanFind(slot.Type, value),
                                     reading,
                                     className);
    }

    private static (string? Reading, string? ClassName) GetDecoded(ArgumentSlot slot,
                                                                   ulong value,
                                                                   TraceArgumentCall call,
                                                                   ValueDescriber? describe)
    {
        var locator = call.GetLocator(slot.Index);

        if (locator is { Kind: not LocatorKind.Referenced } typed)
        {
            return (Display(slot.Type, typed).ToString(), null);
        }

        if (describe?.Invoke(slot, value, call) is { } description)
        {
            return (description.Text,
                    value != 0 && ArgumentValue.IsPointer(slot.Type) ? description.ClassName ?? GetClassName(slot.Type) : null);
        }

        return locator is { } referenced
            ? (Display(slot.Type, referenced).ToString(), null)
            : (ArgumentValue.Decode(slot.Type, value), null);
    }

    private static string GetClassName(string type)
    {
        var indirection = type.IndexOfAny(['*', '&']);

        var words = (indirection < 0 ? type : type[..indirection]).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                                                   .Where(w => w is not ("const" or "volatile" or "__ptr64"
                                                                                        or "enum" or "class" or "struct"));

        return string.Join(' ', words);
    }

    private static void AddSeparator(List<CallSignaturePart> parts, int index)
    {
        if (index > 0)
        {
            parts.Add(new CallSignaturePart(", ", CallSignaturePartKind.Punctuation));
        }
    }

    private static CallSignaturePart RawPart(ulong value, string register)
        => new($"0x{value:X}", CallSignaturePartKind.Value, value, register, value >= SmallestAddress);

    private static Locator? AddressAt(TraceArgumentCall call, int register)
        => call.GetLocator(register) is { Kind: LocatorKind.Page or LocatorKind.Row or LocatorKind.ColumnStoreObject } locator
            ? locator
            : null;

    private static List<ArgumentSlot> CapturedSlots(ArgumentLayout layout) => [.. layout.Slots.Where(TraceArgumentCall.IsCaptured)];

    private static bool HasReturn(string returnType) => returnType != "void" && TraceArgumentCall.IsReturnCaptured(returnType);

    private static object?[] Values(List<ArgumentSlot> slots,
                                    string? returnType,
                                    TraceArgumentCall call,
                                    int index,
                                    ValueDescriber? describe,
                                    bool decode)
    {
        var values = new List<object?> { (long)index + 1, call.ThreadId };

        foreach (var slot in slots)
        {
            values.Add(call.Value(slot) is { } value ? Cell(slot, value, call, describe, decode) : NotCaptured);
        }

        if (returnType is not null)
        {
            values.Add(call.Returned ? Cell(ReturnSlot(returnType), call.ReturnValue, call, describe, decode) : DidNotReturn);
        }

        return [.. values];
    }

    private static object Cell(ArgumentSlot slot,
                               ulong value,
                               TraceArgumentCall call,
                               ValueDescriber? describe,
                               bool decode)
    {
        if (!decode)
        {
            return $"0x{value:X}";
        }

        if (call.GetLocator(slot.Index) is { Kind: not LocatorKind.Referenced } locator)
        {
            return Display(slot.Type, locator);
        }

        var (reading, className) = GetDecoded(slot, value, call, describe);

        if (reading is null)
        {
            return $"0x{value:X}";
        }

        return className is null ? reading : new ObjectValue(className, reading);
    }

    private static ArgumentSlot ReturnSlot(string returnType)
        => new(ReturnName, returnType, ArgumentLocation.Register, LocatorReadResolver.ReturnRegister, ReturnSource(returnType));

    private static object Display(string type, Locator locator) => locator.Kind switch
    {
        LocatorKind.Row => locator.Row,
        LocatorKind.ColumnStoreObject or LocatorKind.LogSequenceNumber or LocatorKind.Text or LocatorKind.WideText or LocatorKind.TypeInfo
            => locator.ToString(),
        LocatorKind.Referenced => ArgumentValue.FormatReferenced(type, locator.Value),
        _ => locator.Page
    };

    private static ResultColumn Column(int ordinal, string name, string type, string detail, Color? tint = null)
        => new(ordinal, name, typeof(string), false) { TypeName = type, Detail = detail, BackgroundColour = tint };

    private static string ReturnSource(string type) => ArgumentValue.IsFloating(type) ? "XMM0" : "RAX";

}
