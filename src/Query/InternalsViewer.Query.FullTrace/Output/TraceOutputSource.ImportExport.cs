using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.FullTrace.Output.Sources;

namespace InternalsViewer.Query.FullTrace.Output;

public abstract partial record TraceOutputSource
{
    internal static void Write(BinaryWriter writer, TraceOutputSource source)
    {
        writer.Write((byte)source.Type);
        writer.Write(source.Value);
        writer.Write(source.Slot);
        writer.Write(source.Owner);

        switch (source)
        {
            case BufferOutputSource buffer:
                writer.Write(buffer.Address);
                break;

            case RowsetOutputSource rowset:
                writer.Write(rowset.Rowset);
                writer.Write(rowset.Ordinal);
                break;

            case HashRecordOutputSource hashRecord:
                writer.Write(hashRecord.Ordinal);
                writer.Write(hashRecord.Offset);
                writer.Write(hashRecord.Length);
                break;
        }
    }

    internal static TraceOutputSource Read(BinaryReader reader)
    {
        var type = (OutputSourceType)reader.ReadByte();

        var value = reader.ReadUInt64();

        var slot = reader.ReadInt32();

        var owner = reader.ReadUInt64();

        TraceOutputSource source = type switch
        {
            OutputSourceType.Buffer => new BufferOutputSource(value, reader.ReadUInt64()),
            OutputSourceType.Rowset => new RowsetOutputSource(value, reader.ReadUInt64()) { Ordinal = reader.ReadInt32() },
            OutputSourceType.HashRecord => new HashRecordOutputSource(value)
            {
                Ordinal = reader.ReadInt32(),
                Offset = reader.ReadInt32(),
                Length = reader.ReadInt32()
            },
            _ => Unknown with { Value = value }
        };

        return source with { Slot = slot, Owner = owner };
    }
}
