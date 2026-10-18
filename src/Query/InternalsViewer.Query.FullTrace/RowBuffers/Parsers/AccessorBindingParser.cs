using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

internal static class AccessorBindingParser
{
    private const int MaximumBindings = 1024;

    private const ulong MaximumOffset = 0x10000;

    private const ulong BindingSize = 0x58;

    private const ulong OrdinalOffset = 0x00;

    private const ulong ValueOffsetOffset = 0x08;

    private const ulong TypeInfoOffset = 0x20;

    private const ulong PartOffset = 0x38;

    private const uint ValuePart = 0x1;

    public static Binding[] Parse(ITraceReplay replay, ulong bindings, ulong count)
    {
        if (count is 0 or > MaximumBindings || bindings < ColumnValueParser.SmallestAddress)
        {
            return [];
        }

        var parsed = new List<Binding>();

        for (var index = 0UL; index < count; index++)
        {
            if (ParseBinding(replay, bindings + index * BindingSize) is { } binding)
            {
                parsed.Add(binding);
            }
        }

        return [.. parsed];
    }

    private static Binding? ParseBinding(ITraceReplay replay, ulong binding)
    {
        if (!replay.TryRead(binding + OrdinalOffset, out ulong ordinal)
            || !replay.TryRead(binding + ValueOffsetOffset, out ulong valueOffset)
            || !replay.TryRead(binding + PartOffset, out uint part)
            || (part & ValuePart) == 0
            || ordinal > MaximumBindings
            || valueOffset >= MaximumOffset)
        {
            return null;
        }

        var type = replay.TryRead(binding + TypeInfoOffset, out ulong typeInfo)
            ? RowBufferTypeParser.Parse(replay, typeInfo, (int)ordinal)
            : null;

        return new Binding((int)ordinal, valueOffset, type);
    }

    internal readonly record struct Binding(int Ordinal, ulong ValueOffset, RowBufferType? Type);
}
