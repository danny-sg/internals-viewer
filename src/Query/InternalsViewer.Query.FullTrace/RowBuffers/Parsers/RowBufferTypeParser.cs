using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

internal static class RowBufferTypeParser
{
    private const ulong TypeIdOffset = 0x04;

    private const ulong LengthOffset = 0x10;

    private const ulong PrecisionOffset = 0x12;

    private const ulong ScaleOffset = 0x13;

    private const ulong CollationOffset = 0x14;

    public static RowBufferType? Parse(ITraceReplay replay, ulong typeInfo, int ordinal)
    {
        if (typeInfo < ColumnValueParser.SmallestAddress
            || !replay.TryRead(typeInfo + TypeIdOffset, out byte typeId)
            || !SqlTypeHelpers.TryGetSqlType(typeId, out _)
            || !replay.TryRead(typeInfo + LengthOffset, out ushort length)
            || !replay.TryRead(typeInfo + PrecisionOffset, out byte precision)
            || !replay.TryRead(typeInfo + ScaleOffset, out byte scale)
            || !replay.TryRead(typeInfo + CollationOffset, out uint collation))
        {
            return null;
        }

        return new RowBufferType(ordinal, typeId, length, precision, scale, collation);
    }
}
