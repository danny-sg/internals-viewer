using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.Results;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Arguments.Enums;

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

    public static QueryResultSet BuildTable(ArgumentLayout layout,
                                            IReadOnlyList<TraceArgumentCall> calls,
                                            ValueDescriber? describe = null,
                                            IReadOnlyList<int>? included = null,
                                            Func<bool>? isDecoded = null)
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
            columns.Add(BuildColumn(columns.Count, slot.Name, slot.Type, slot.Source));
        }

        if (hasReturn)
        {
            columns.Add(BuildColumn(columns.Count, ReturnName, returnType, GetReturnSource(returnType), ReturnTint));
        }

        var returned = hasReturn ? returnType : null;

        var source = new CachedRowSource<TraceArgumentCall>(calls,
                                                            columns.Count,
                                                            (call, index) => GetValues(slots,
                                                                                       returned,
                                                                                       call,
                                                                                       index,
                                                                                       describe,
                                                                                       isDecoded?.Invoke() ?? true));

        var rows = new ResultRow<long>[included?.Count ?? calls.Count];

        for (var index = 0; index < rows.Length; index++)
        {
            var callIndex = included?[index] ?? index;

            rows[index] = new ResultRow<long>(source, callIndex) { Id = callIndex };
        }

        return new QueryResultSet { Columns = columns, Rows = rows, Source = source };
    }

    public static ArgumentSlot? FindSlotAt(ArgumentLayout layout, int ordinal)
    {
        var slots = CapturedSlots(layout);

        var position = ordinal - FixedColumns;

        return position >= 0 && position < slots.Count ? slots[position] : null;
    }

    public static (string Name, int Register)? GetHookArgument(ArgumentLayout layout, int ordinal)
    {
        var slots = CapturedSlots(layout);

        var position = ordinal - FixedColumns;

        if (position >= 0 && position < slots.Count)
        {
            return slots[position] is { Location: ArgumentLocation.Register or ArgumentLocation.Stack } slot
                ? (slot.Name, slot.Index)
                : null;
        }

        return position == slots.Count && layout.ReturnType is { } returnType && HasReturn(returnType)
            ? (ReturnName, ArgumentLayout.ReturnRegister)
            : null;
    }

    public static ArgumentCell GetCellAt(ArgumentLayout layout, TraceArgumentCall call, int ordinal)
    {
        var slots = CapturedSlots(layout);

        var position = ordinal - FixedColumns;

        if (position >= 0 && position < slots.Count)
        {
            return new ArgumentCell(call.GetValue(slots[position]), AddressAt(call, slots[position].Index), slots[position].Type);
        }

        if (position == slots.Count && layout.ReturnType is { } returnType && HasReturn(returnType) && call.Returned)
        {
            return new ArgumentCell(call.ReturnValue, AddressAt(call, ArgumentLayout.ReturnRegister), returnType);
        }

        return default;
    }

    public static string? DecodedTextAt(ArgumentLayout layout, TraceArgumentCall call, int ordinal, ValueDescriber? describe)
        => FindValueAt(layout, call, ordinal) is { } found ? GetCellValue(found.Slot, found.Value, call, describe, decode: true).ToString() : null;

    public static MemoryMapEntry? OriginAt(ArgumentLayout layout, TraceArgumentCall call, int ordinal, ValueDescriber describe)
        => FindValueAt(layout, call, ordinal) is { } found ? GetDecoded(found.Slot, found.Value, call, describe).Origin : null;

    public static IReadOnlyList<CallSignaturePart> GetSignature(string function,
                                                                ArgumentLayout? layout,
                                                                TraceArgumentCall call,
                                                                ValueDescriber? describe = null,
                                                                bool includeReturn = true,
                                                                bool includeTypes = false,
                                                                bool showThisAddress = true)
    {
        var parts = new List<CallSignaturePart>
        {
            new(function, CallSignaturePartKind.Function),
            new("(", CallSignaturePartKind.Punctuation)
        };

        if (layout is null)
        {
            for (var index = 0; index < ArgumentLayout.RegisterCount; index++)
            {
                AddSeparator(parts, index);

                parts.Add(RawPart(call.Slot(index), ArgumentLayout.Registers[index]));
            }

            parts.Add(new CallSignaturePart(")", CallSignaturePartKind.Punctuation));

            if (includeReturn && ReturnOf(layout, call, describe, includeTypes) is { } returned)
            {
                parts.Add(new CallSignaturePart(ReturnArrow, CallSignaturePartKind.Punctuation));

                parts.Add(returned);
            }

            return parts;
        }

        var argument = 0;

        for (var index = 0; index < layout.Slots.Count; index++)
        {
            var slot = layout.Slots[index];

            AddSeparator(parts, index);

            var label = slot.Type == ThisType ? CallSignaturePart.ThisLabel
                        : slot.Name == ArgumentLayout.ReturnBuffer ? slot.Name
                        : $"{++argument}";

            var part = call.GetValue(slot) is { } value
                ? ValuePart(slot, value, call, describe, includeTypes, label)
                : new CallSignaturePart(slot.Type, CallSignaturePartKind.NotCaptured, toolTip: $"Not Captured ({slot.Source})");

            if (!showThisAddress && slot.Type == ThisType && (part.Decoded is null || part.Decoded == part.Text))
            {
                parts.Add(new CallSignaturePart(slot.Name, CallSignaturePartKind.Name));

                continue;
            }

            if (slot.Name.Length > 0)
            {
                parts.Add(new CallSignaturePart($"{slot.Name} ", CallSignaturePartKind.Name));
            }

            parts.Add(part);
        }

        parts.Add(new CallSignaturePart(")", CallSignaturePartKind.Punctuation));

        if (includeReturn && ReturnOf(layout, call, describe, includeTypes) is { } returnValue)
        {
            parts.Add(new CallSignaturePart(ReturnArrow, CallSignaturePartKind.Punctuation));

            parts.Add(returnValue);
        }

        return parts;
    }

    public static CallSignaturePart? ThisOf(ArgumentLayout? layout, TraceArgumentCall call, ValueDescriber? describe = null)
        => layout?.Slots.FirstOrDefault(s => s.Type == ThisType) is { } slot && call.GetValue(slot) is { } value
            ? ValuePart(slot, value, call, describe, label: CallSignaturePart.ThisLabel)
            : null;

    public static CallSignaturePart? ReturnOf(ArgumentLayout? layout,
                                              TraceArgumentCall call,
                                              ValueDescriber? describe = null,
                                              bool includeType = false)
    {
        if (!call.Returned)
        {
            return null;
        }

        if (layout is null)
        {
            return RawPart(call.ReturnValue, "RAX");
        }

        return layout.ReturnType is { } returnType && HasReturn(returnType)
            ? ValuePart(BuildReturnSlot(returnType), call.ReturnValue, call, describe, includeType, ReturnName)
            : null;
    }

    public static bool CanFind(string type, ulong value)
        => value != 0 && (type == ThisType || ArgumentValue.IsPointer(type) || value >= SmallestAddress);

    private static (ArgumentSlot Slot, ulong Value)? FindValueAt(ArgumentLayout layout, TraceArgumentCall call, int ordinal)
    {
        if (FindSlotAt(layout, ordinal) is { } slot)
        {
            return call.GetValue(slot) is { } value ? (slot, value) : null;
        }

        return GetCellAt(layout, call, ordinal) is { Value: { } returned, Type: { } type } ? (BuildReturnSlot(type), returned) : null;
    }

    private static CallSignaturePart ValuePart(ArgumentSlot slot,
                                               ulong value,
                                               TraceArgumentCall call,
                                               ValueDescriber? describe,
                                               bool includeType = false,
                                               string? label = null)
    {
        var (reading, className, origin) = GetDecoded(slot, value, call, describe);

        return new CallSignaturePart($"0x{value:X}",
                                     CallSignaturePartKind.Value,
                                     value,
                                     $"{slot.Type} ({slot.Source})",
                                     CanFind(slot.Type, value),
                                     reading,
                                     className,
                                     call.GetLocator(slot.Index) is { } locator ? GetDisplayValue(slot.Type, locator) as StringValue : null,
                                     includeType && slot.Type != ThisType ? slot.Type : null,
                                     label,
                                     slot.Type == ThisType ? null : slot.Type,
                                     origin,
                                     slot.Source);
    }

    private static (string? Reading, string? ClassName, MemoryMapEntry? Origin) GetDecoded(ArgumentSlot slot,
                                                                                           ulong value,
                                                                                           TraceArgumentCall call,
                                                                                           ValueDescriber? describe)
    {
        var locator = call.GetLocator(slot.Index);

        if (locator is { ValueType: not LocatorValueType.Referenced } typed)
        {
            return (GetDisplayValue(slot.Type, typed).ToString(), null, null);
        }

        if (describe?.Invoke(slot, value, call) is { } description)
        {
            return (description.Text,
                    value != 0 && ArgumentValue.IsPointer(slot.Type) ? description.ClassName ?? GetClassName(slot.Type) : null,
                    description.Origin);
        }

        return locator is { } referenced
            ? (GetDisplayValue(slot.Type, referenced).ToString(), null, null)
            : (ArgumentValue.Decode(slot.Type, value), null, null);
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
        => new($"0x{value:X}", CallSignaturePartKind.Value, value, register, value >= SmallestAddress, location: register);

    private static Locator? AddressAt(TraceArgumentCall call, int register)
        => call.GetLocator(register) is { ValueType: LocatorValueType.Page or LocatorValueType.Row or LocatorValueType.ColumnStoreObject } locator
            ? locator
            : null;

    private static List<ArgumentSlot> CapturedSlots(ArgumentLayout layout) => [.. layout.Slots.Where(TraceArgumentCall.IsCaptured)];

    private static bool HasReturn(string returnType) => returnType != "void" && TraceArgumentCall.IsReturnCaptured(returnType);

    private static object?[] GetValues(List<ArgumentSlot> slots,
                                       string? returnType,
                                       TraceArgumentCall call,
                                       int index,
                                       ValueDescriber? describe,
                                       bool decode)
    {
        var values = new List<object?> { (long)index + 1, call.ThreadId };

        foreach (var slot in slots)
        {
            values.Add(call.GetValue(slot) is { } value ? GetCellValue(slot, value, call, describe, decode) : NotCaptured);
        }

        if (returnType is not null)
        {
            values.Add(call.Returned ? GetCellValue(BuildReturnSlot(returnType), call.ReturnValue, call, describe, decode) : DidNotReturn);
        }

        return [.. values];
    }

    private static object GetCellValue(ArgumentSlot slot,
                                       ulong value,
                                       TraceArgumentCall call,
                                       ValueDescriber? describe,
                                       bool decode)
    {
        if (!decode)
        {
            return $"0x{value:X}";
        }

        if (call.GetLocator(slot.Index) is { ValueType: not LocatorValueType.Referenced } locator)
        {
            return GetDisplayValue(slot.Type, locator);
        }

        var (reading, className, origin) = GetDecoded(slot, value, call, describe);

        if (reading is null)
        {
            return $"0x{value:X}";
        }

        return className is null && origin is null ? reading : new ObjectValue(className, reading) { Origin = origin };
    }

    private static ArgumentSlot BuildReturnSlot(string returnType)
        => new(ReturnName, returnType, ArgumentLocation.Register, ArgumentLayout.ReturnRegister, GetReturnSource(returnType));

    private static object GetDisplayValue(string type, Locator locator) => locator.ValueType switch
    {
        LocatorValueType.Row
            => locator.Row,
        LocatorValueType.Text or LocatorValueType.WideText
            => new StringValue(new TextLocator(locator).Text, new TextLocator(locator).IsTruncated),
        LocatorValueType.ColumnStoreObject or LocatorValueType.LogSequenceNumber or LocatorValueType.TypeInfo or LocatorValueType.Variant
            => locator.ToString(),
        LocatorValueType.Referenced => ArgumentValue.FormatReferenced(type, locator.Value),
        _ => locator.Page
    };

    private static ResultColumn BuildColumn(int ordinal, string name, string type, string detail, Color? tint = null)
        => new(ordinal, name, typeof(string), false) { TypeName = type, Detail = detail, BackgroundColour = tint };

    private static string GetReturnSource(string type) => ArgumentValue.IsFloating(type) ? "XMM0" : "RAX";

}
