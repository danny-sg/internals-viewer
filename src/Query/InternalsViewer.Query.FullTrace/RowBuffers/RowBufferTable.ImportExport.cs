namespace InternalsViewer.Query.FullTrace.RowBuffers;

internal sealed partial class RowBufferTable
{
    internal void Write(BinaryWriter writer)
    {
        writer.Write(Rows.Length);

        foreach (var row in Rows)
        {
            writer.Write(row.Call);

            writer.Write(row.Id);

            writer.Write(row.Iterator);

            writer.Write(row.Buffer);

            writer.Write(row.Layout);

            writer.Write(row.FirstColumn);

            writer.Write((byte)row.Format);
        }

        writer.Write(Values.Length);

        foreach (var value in Values)
        {
            writer.Write(value.Offset);

            writer.Write(value.Length);

            writer.Write(value.Status);

            writer.Write((byte)value.Flags);
        }

        writer.Write(Bindings.Length);

        foreach (var bindings in Bindings)
        {
            writer.Write(bindings.Length);

            foreach (var binding in bindings)
            {
                writer.Write(binding.RecordOffset);

                writer.Write(binding.Flags);

                writer.Write(binding.FixedLength);
            }
        }

        writer.Write(Data.Length);

        writer.Write(Data);
    }

    internal static RowBufferTable Read(BinaryReader reader, ulong function, ulong instance, int version)
    {
        var rows = new Row[reader.ReadInt32()];

        for (var index = 0; index < rows.Length; index++)
        {
            rows[index] = new Row(reader.ReadInt32(),
                                  reader.ReadUInt64(),
                                  reader.ReadUInt64(),
                                  reader.ReadUInt64(),
                                  reader.ReadInt32(),
                                  reader.ReadInt32(),
                                  version >= RowBufferIndex.FormatVersion ? (RowBufferFormat)reader.ReadByte() : RowBufferFormat.Bindings);
        }

        var values = new Value[reader.ReadInt32()];

        for (var index = 0; index < values.Length; index++)
        {
            values[index] = new Value(reader.ReadInt32(), reader.ReadInt32(), reader.ReadByte(), (RowBufferColumnFlags)reader.ReadByte());
        }

        var bindings = new Binding[reader.ReadInt32()][];

        for (var layout = 0; layout < bindings.Length; layout++)
        {
            bindings[layout] = new Binding[reader.ReadInt32()];

            for (var ordinal = 0; ordinal < bindings[layout].Length; ordinal++)
            {
                bindings[layout][ordinal] = new Binding(reader.ReadInt32(), reader.ReadUInt32(), reader.ReadInt32());
            }
        }

        var data = reader.ReadBytes(reader.ReadInt32());

        return new RowBufferTable(function, instance, rows, values, bindings, data);
    }
}
