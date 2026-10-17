using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

internal sealed class BindingParser
{
    public const uint ByReferenceBinding = 0x800;

    public const uint LengthBinding = 0x100;

    public const uint LobBinding = 0x4000;

    private const ulong LayoutCountOffset = 0x62;

    private const ulong LayoutArrayOffset = 0x70;

    private const ulong LayoutBindingSize = 0x50;

    private const ulong LayoutRecordOffset = 0x00;

    private const ulong LayoutValueOffset = 0x28;

    private const ulong LayoutStatusOffset = 0x30;

    private const ulong LayoutFixedLengthOffset = 0x3C;

    private const ulong LayoutFlagsOffset = 0x40;

    private const ulong LayoutInfoOffset = 0x48;

    private const ulong LayoutInfoLengthOffset = 0x18;

    private const ulong LayoutInfoFlagsOffset = 0x2A;

    private const byte AlternateBufferBinding = 0x10;

    private const int MaximumBindings = 1024;

    private Dictionary<ulong, BindingSet> BindingSets { get; } = [];

    private Dictionary<ulong, BindingSet> KnownSets { get; } = [];

    public BindingSet? Parse(ITraceReplay replay, ulong bindings)
    {
        if (!replay.TryRead(bindings + LayoutCountOffset, out ushort count)
            || !replay.TryRead(bindings + LayoutArrayOffset, out ulong array)
            || count == 0
            || count > MaximumBindings
            || array < ColumnValueParser.SmallestAddress)
        {
            return KnownSets.GetValueOrDefault(bindings);
        }

        if (!replay.TryRead(array + LayoutValueOffset, out ulong firstValueOffset)
            || !replay.TryRead(array + LayoutFlagsOffset, out uint firstFlags))
        {
            return KnownSets.GetValueOrDefault(bindings);
        }

        if (BindingSets.TryGetValue(array, out var set)
            && set.Count == count
            && set.FirstValueOffset == firstValueOffset
            && set.FirstFlags == firstFlags)
        {
            KnownSets[bindings] = set;

            return set;
        }

        set = new BindingSet(count,
                             firstValueOffset,
                             firstFlags,
                             [.. Enumerable.Range(0, count).Select(i => ParseBinding(replay, array + (ulong)i * LayoutBindingSize))]);

        if (set.Columns.All(c => c.Unreadable != RowBufferColumnFlags.BindingUnreadable))
        {
            BindingSets[array] = set;

            KnownSets[bindings] = set;
        }

        return set;
    }

    private static ColumnBinding ParseBinding(ITraceReplay replay, ulong address)
    {
        if (!replay.TryRead(address + LayoutRecordOffset, out int recordOffset))
        {
            recordOffset = -1;
        }

        ushort fixedLength = 0;
        ulong valueOffset = 0;
        ulong statusOffset = 0;
        ulong lengthOffset = 0;
        byte infoFlags = 0;

        var isReadable = replay.TryRead(address + LayoutFlagsOffset, out uint flags)
                         && replay.TryRead(address + LayoutFixedLengthOffset, out fixedLength)
                         && replay.TryRead(address + LayoutValueOffset, out valueOffset)
                         && replay.TryRead(address + LayoutStatusOffset, out statusOffset)
                         && replay.TryRead(address + LayoutInfoOffset, out ulong info)
                         && info >= ColumnValueParser.SmallestAddress
                         && replay.TryRead(info + LayoutInfoFlagsOffset, out infoFlags)
                         && ((flags & LengthBinding) == 0 || replay.TryRead(info + LayoutInfoLengthOffset, out lengthOffset));

        var unreadable = !isReadable ? RowBufferColumnFlags.BindingUnreadable
                         : (infoFlags & AlternateBufferBinding) != 0 ? RowBufferColumnFlags.AlternateBuffer
                         : RowBufferColumnFlags.None;

        return new ColumnBinding(new RowBufferBinding(recordOffset, flags, fixedLength, valueOffset),
                                 unreadable,
                                 valueOffset,
                                 statusOffset,
                                 lengthOffset);
    }

    internal readonly record struct ColumnBinding(RowBufferBinding Binding,
                                                  RowBufferColumnFlags Unreadable,
                                                  ulong ValueOffset,
                                                  ulong StatusOffset,
                                                  ulong LengthOffset);

    internal sealed record BindingSet(ushort Count, ulong FirstValueOffset, uint FirstFlags, ColumnBinding[] Columns);
}
