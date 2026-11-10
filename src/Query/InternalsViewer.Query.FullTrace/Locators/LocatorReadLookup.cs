using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;
using InternalsViewer.Query.FullTrace.Locators.Enums;

namespace InternalsViewer.Query.FullTrace.Locators;

public static class LocatorReadLookup
{
    private const int StackCandidateParameters = 4;

    internal static void AddReads(List<LocatorRead> reads, ulong function, FunctionSignature signature, ArgumentLayout layout)
    {
        var slots = layout.Slots;

        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];

            if (slot.Location is not (ArgumentLocation.Register or ArgumentLocation.Stack)
                || slot.Name == ArgumentLayout.ReturnBuffer
                || LocatorType.GetLocatorType(slot.Type) is not { } type
                || (slot.Location == ArgumentLocation.Stack && type.ValueType == LocatorValueType.Referenced))
            {
                continue;
            }

            var count = type.ValueType == LocatorValueType.Bindings ? BindingArgumentLayout.GetCountIndex(slots, index) : -1;

            var countArgumentSlot = count < 0 ? -1 : slots[count].Index;

            AddArgumentSlotReads(reads, function, slot.Index, type, returned: false, countArgumentSlot);
        }

        if (signature.ReturnType is { } returnType && LocatorType.GetLocatorType(returnType) is { } returned)
        {
            AddArgumentSlotReads(reads, function, ArgumentLayout.ReturnArgumentSlot, returned, returned: true);
        }
    }

    internal static int GetStackArguments(int? parameterCount, ArgumentLayout layout)
        => parameterCount is >= StackCandidateParameters ? GetStackSlotCount(layout) : 0;

    internal static int GetStackSlotCount(ArgumentLayout layout)
    {
        var count = 0;

        for (var index = 0; index < layout.Slots.Count; index++)
        {
            if (layout.Slots[index].Location == ArgumentLocation.Stack)
            {
                count++;
            }
        }

        return count;
    }

    private static void AddArgumentSlotReads(List<LocatorRead> reads,
                                         ulong function,
                                         int argumentSlot,
                                         LocatorType type,
                                         bool returned,
                                         int countArgumentSlot = -1)
    {
        if (returned || type.ValueType == LocatorValueType.Variant)
        {
            reads.Add(new LocatorRead(function, argumentSlot, true, type.ValueType, type.Offset, type.Path, countArgumentSlot));

            return;
        }

        if (type.IsInOut)
        {
            reads.Add(new LocatorRead(function, argumentSlot, false, type.ValueType, type.Offset, type.Path, countArgumentSlot));
        }

        reads.Add(new LocatorRead(function, argumentSlot, type.IsInOut || type.IsOutput, type.ValueType, type.Offset, type.Path, countArgumentSlot));
    }
}
