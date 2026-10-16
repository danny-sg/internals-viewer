using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed partial class RowBufferStore
{
    private const int FormatVersion = 4;

    private const int NoIndex = -1;

    private const byte NoDeferredType = 0;

    private const byte DeferredPageType = 1;

    private const byte DeferredLobType = 2;

    internal void Write(BinaryWriter writer)
    {
        var rows = Rows.Values.SelectMany(r => r).ToArray();

        var columnSets = GetIndexes(rows.Where(r => r.Record is null).Select(r => r.Columns));


        var bindings = GetIndexes(columnSets.Keys.SelectMany(c => c).Select(c => c.Binding));

        writer.Write(-FormatVersion);

        writer.Write(bindings.Count);

        foreach (var binding in bindings.Keys)
        {
            writer.Write(binding.RecordOffset);

            writer.Write(binding.Flags);

            writer.Write(binding.FixedLength);

            writer.Write(binding.ValueOffset);
        }

        writer.Write(columnSets.Count);

        foreach (var columns in columnSets.Keys)
        {
            writer.Write(columns.Count);

            foreach (var column in columns)
            {
                WriteColumn(writer, column, bindings[column.Binding]);
            }
        }

        writer.Write(rows.Length);

        foreach (var row in rows)
        {
            writer.Write(row.Function);

            writer.Write(row.Instance);

            writer.Write(row.Call);

            writer.Write(row.Position.Sequence);

            writer.Write(row.Position.Steps);

            writer.Write(row.Iterator);

            writer.Write(row.Address);

            writer.Write((byte)row.Format);

            writer.Write(row.Record is null ? columnSets[row.Columns] : NoIndex);

            writer.Write(row.Batch);

            writer.Write(row.BatchSelectionIndex);

            writer.Write(row.Record?.Length ?? NoIndex);

            if (row.Record is { } record)
            {
                writer.Write(record);
            }
        }
    }

    internal static RowBufferStore Read(BinaryReader reader)
    {
        if (reader.ReadInt32() != -FormatVersion)
        {
            throw new InvalidDataException("The row buffers were exported in an older format and must be re-recorded");
        }

        var bindings = new RowBufferBinding[reader.ReadInt32()];

        for (var index = 0; index < bindings.Length; index++)
        {
            bindings[index] = new RowBufferBinding(reader.ReadInt32(), reader.ReadUInt32(), reader.ReadInt32(), reader.ReadUInt64());
        }

        var columnSets = new RowBufferColumn[reader.ReadInt32()][];

        for (var index = 0; index < columnSets.Length; index++)
        {
            var columns = new RowBufferColumn[reader.ReadInt32()];

            for (var ordinal = 0; ordinal < columns.Length; ordinal++)
            {
                columns[ordinal] = ReadColumn(reader, ordinal, bindings);
            }

            columnSets[index] = columns;
        }

        var rows = new RowBuffer[reader.ReadInt32()];

        for (var index = 0; index < rows.Length; index++)
        {
            var function = reader.ReadUInt64();

            var instance = reader.ReadUInt64();

            var call = reader.ReadInt32();

            var position = new TracePosition(reader.ReadUInt64(), reader.ReadUInt64());

            var iterator = reader.ReadUInt64();

            var address = reader.ReadUInt64();

            var format = (RowBufferFormat)reader.ReadByte();

            var columnSet = reader.ReadInt32();

            var batch = reader.ReadUInt64();

            var selectionIndex = reader.ReadInt32();

            var recordLength = reader.ReadInt32();

            var record = recordLength >= 0 ? reader.ReadBytes(recordLength) : null;

            var columns = record is null ? columnSets[columnSet] : RecordRowParser.Split(record, null);

            rows[index] = new RowBuffer(function, instance, call, position, iterator, address, columns, format)
            {
                Batch = batch,
                BatchSelectionIndex = selectionIndex,
                Record = record
            };
        }

        return new RowBufferStore(rows);
    }

    private static Dictionary<T, int> GetIndexes<T>(IEnumerable<T> items) where T : class
    {
        var indexes = new Dictionary<T, int>(ReferenceEqualityComparer.Instance);

        foreach (var item in items)
        {
            indexes.TryAdd(item, indexes.Count);
        }

        return indexes;
    }

    private static void WriteColumn(BinaryWriter writer, RowBufferColumn column, int binding)
    {
        writer.Write(binding);

        writer.Write(column.Status);

        writer.Write((byte)column.Flags);

        writer.Write(column.Length);

        writer.Write(column.Data.Length);

        writer.Write(column.Data);

        writer.Write(column.Pointer);

        switch (column.Deferred)
        {
            case DeferredPageValue page:
                writer.Write(DeferredPageType);

                writer.Write(page.Page.FileId);

                writer.Write(page.Page.PageId);

                writer.Write(page.Offset);

                writer.Write(page.Lsn.VirtualLogFile);

                writer.Write(page.Lsn.FileOffset);

                writer.Write(page.Lsn.RecordSequence);

                break;

            case DeferredLobValue lob:
                writer.Write(DeferredLobType);

                writer.Write(lob.Links.Count);

                foreach (var link in lob.Links)
                {
                    writer.Write(link.Offset);

                    writer.Write(link.Page.FileId);

                    writer.Write(link.Page.PageId);

                    writer.Write(link.Slot);
                }

                break;

            default:
                writer.Write(NoDeferredType);

                break;
        }

        writer.Write(column.DeepDataVectorId);
    }

    private static RowBufferColumn ReadColumn(BinaryReader reader, int ordinal, RowBufferBinding[] bindings)
    {
        var column = new RowBufferColumn(ordinal,
                                         bindings[reader.ReadInt32()],
                                         reader.ReadByte(),
                                         (RowBufferColumnFlags)reader.ReadByte(),
                                         reader.ReadInt32(),
                                         reader.ReadBytes(reader.ReadInt32()))
        {
            Pointer = reader.ReadUInt64()
        };

        column = reader.ReadByte() switch
        {
            DeferredPageType => column with { Deferred = ReadDeferredPage(reader) },
            DeferredLobType => column with { Deferred = ReadDeferredLob(reader) },
            _ => column
        };

        return column with { DeepDataVectorId = reader.ReadInt32() };
    }

    private static DeferredPageValue ReadDeferredPage(BinaryReader reader)
    {
        var page = new PageAddress(reader.ReadInt16(), reader.ReadInt32());

        var offset = reader.ReadInt32();

        var lsn = new LogSequenceNumber(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt16());

        return new DeferredPageValue(page, offset, lsn);
    }

    private static DeferredLobValue ReadDeferredLob(BinaryReader reader)
    {
        var links = new DeferredLobLink[reader.ReadInt32()];

        for (var index = 0; index < links.Length; index++)
        {
            var offset = reader.ReadInt32();

            var page = new PageAddress(reader.ReadInt16(), reader.ReadInt32());

            links[index] = new DeferredLobLink(offset, page, reader.ReadInt32());
        }

        return new DeferredLobValue(links);
    }
}
