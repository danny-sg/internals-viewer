using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.Extensions;

namespace InternalsViewer.Query.FullTrace.Output;

/// <summary>
/// Gets the address an output column of the query reads its data from
/// </summary>
/// <remarks>
/// For the iterator model GetRow doesn't actually return rows. Instead a reference based path is defined for the query which can point
/// to different structures. As the query iterates pointers in these addresses can be updated so when the row is output all of the
/// references are pointing to results that have been collected throughout the operators, e.g. row buffers, hash table entries etc.
///
/// This parser follows the path for each output column of the query and returns the address at its end, so the iterator each column
/// comes from can be found.
/// </remarks>
internal static class OutputSourceParser
{
    private const int MaximumDepth = 4;

    private const ulong RowBufferVariantOffset = 0x08;
    private const ulong TableRowsetOffset = 0x08;
    private const ulong HashSlotOffset = 0x2C;
    private const ulong VariantTableSlotOffset = 0x08;
    private const ulong VariantTableOrdinalOffset = 0x0C;
    private const ulong BatchRedirectHelperOffset = 0x10;
    private const ulong HashSwitchInnerOffset = 0x10;
    private const ulong HashCachedSwitchInnerOffset = 0x08;

    public static ulong GetAddress(ITraceReplay replay,
                                   IReadOnlyDictionary<ulong, OutputValueClass> classes,
                                   ulong value,
                                   ulong context,
                                   int depth = 0)
    {
        if (depth > MaximumDepth || !replay.TryRead(value, out ulong vtable) || !classes.TryGetValue(vtable, out var valueClass))
        {
            return 0;
        }

        return valueClass switch
        {
            OutputValueClass.RowBuffer
                => Read(replay, value + RowBufferVariantOffset),
            OutputValueClass.Table
                => Read(replay, value + TableRowsetOffset),
            OutputValueClass.Hash
                => GetSlotRow(replay, value + HashSlotOffset, context),
            OutputValueClass.VariantTable
                => GetVariantRow(replay, value, context),
            OutputValueClass.HashSwitch
                => GetAddress(replay, classes, Read(replay, value + HashSwitchInnerOffset), context, depth + 1),
            OutputValueClass.HashCachedSwitch
                => GetAddress(replay, classes, Read(replay, value + HashCachedSwitchInnerOffset), context, depth + 1),
            OutputValueClass.BatchRedirect
                => Read(replay, value + BatchRedirectHelperOffset),
            _ => 0
        };
    }

    private static ulong GetVariantRow(ITraceReplay replay, ulong value, ulong context)
        => GetSlotRow(replay, value + VariantTableSlotOffset, context) is not 0 and var row
           && replay.TryRead(value + VariantTableOrdinalOffset, out int ordinal)
           && ordinal >= 0
            ? row + (ulong)ordinal * VariantLocator.Size
            : 0;

    private static ulong GetSlotRow(ITraceReplay replay, ulong slotAddress, ulong context)
        => context >= TraceAddress.MinAddress
           && replay.TryRead(slotAddress, out int slot)
           && slot >= 0
            ? Read(replay, context + (ulong)slot * sizeof(ulong))
            : 0;

    private static ulong Read(ITraceReplay replay, ulong address)
        => address >= TraceAddress.MinAddress && replay.TryRead(address, out ulong value) && value >= TraceAddress.MinAddress ? value : 0;
}
