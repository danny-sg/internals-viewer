using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimelineRow
{
    internal static TraceTimelineRow Read(BinaryReader reader,
                                          TraceScale scale,
                                          TraceThreadClock clock,
                                          TraceSpanStore store,
                                          int offset)
    {
        var count = ExportBinary.ReadArray(reader, store.Froms, offset);

        ReadColumn(reader, store.Tos, offset, count);

        ReadColumn(reader, store.CallNodeIndexes, offset, count);

        ReadColumn(reader, store.Calls, offset, count);

        ReadColumn(reader, store.Flags, offset, count);

        return new TraceTimelineRow(scale, clock, store, offset, count);
    }

    internal void Write(BinaryWriter writer)
    {
        WriteColumn(writer, Store.Froms);

        WriteColumn(writer, Store.Tos);

        WriteColumn(writer, Store.CallNodeIndexes);

        WriteColumn(writer, Store.Calls);

        WriteColumn(writer, Store.Flags);
    }

    private static void ReadColumn<T>(BinaryReader reader, T[] column, int offset, int count) where T : unmanaged
    {
        if (ExportBinary.ReadArray(reader, column, offset) != count)
        {
            throw new InvalidDataException("The trace timeline row columns differ in length");
        }
    }

    private void WriteColumn<T>(BinaryWriter writer, T[] column) where T : unmanaged
    {
        if (Indexes is null)
        {
            ExportBinary.WriteArray<T>(writer, column.AsSpan(Offset, Count));
        }
        else
        {
            var values = new T[Count];

            for (var index = 0; index < Count; index++)
            {
                values[index] = column[Indexes[Offset + index]];
            }

            ExportBinary.WriteArray(writer, values);
        }
    }
}
