using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Output;

internal static class OutputSourceParser
{
    private const int MaximumDepth = 4;

    private const ulong TableSlotOffset = 0x10;

    private const ulong HashSlotOffset = 0x2C;

    private const ulong VariantTableSlotOffset = 0x08;

    private const ulong BatchRedirectHelperOffset = 0x10;

    private const ulong HashSwitchInnerOffset = 0x10;

    private const ulong HashCachedSwitchInnerOffset = 0x08;

    public static OutputSource Parse(ITraceReplay replay,
                                     IReadOnlyDictionary<ulong, OutputValueClass> classes,
                                     ulong value,
                                     ulong context,
                                     int depth = 0)
    {
        if (depth > MaximumDepth || !replay.TryRead(value, out ulong vtable) || !classes.TryGetValue(vtable, out var valueClass))
        {
            return OutputSource.Unknown;
        }

        if (GetInner(replay, value, valueClass) is { } inner)
        {
            return Parse(replay, classes, inner, context, depth + 1) with { Value = value };
        }

        return valueClass switch
        {
            OutputValueClass.Table => GetSlotSource(replay, value, context, TableSlotOffset),
            OutputValueClass.Hash => GetSlotSource(replay, value, context, HashSlotOffset),
            OutputValueClass.VariantTable => GetSlotSource(replay, value, context, VariantTableSlotOffset),
            OutputValueClass.BatchRedirect => replay.TryRead(value + BatchRedirectHelperOffset, out ulong helper)
                ? new OutputSource(value) { Owner = helper }
                : OutputSource.Unknown,
            OutputValueClass.Constant => new OutputSource(value),
            _ => OutputSource.Unknown
        };
    }

    private static ulong? GetInner(ITraceReplay replay, ulong value, OutputValueClass valueClass)
    {
        var offset = valueClass switch
        {
            OutputValueClass.HashSwitch => HashSwitchInnerOffset,
            OutputValueClass.HashCachedSwitch => HashCachedSwitchInnerOffset,
            _ => 0UL
        };

        return offset != 0 && replay.TryRead(value + offset, out ulong inner) ? inner : null;
    }

    private static OutputSource GetSlotSource(ITraceReplay replay, ulong value, ulong context, ulong slotOffset)
        => replay.TryRead(value + slotOffset, out int slot)
            ? new OutputSource(value) { Slot = slot, Context = context }
            : OutputSource.Unknown;
}
