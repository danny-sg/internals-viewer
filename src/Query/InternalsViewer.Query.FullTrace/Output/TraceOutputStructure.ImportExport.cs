using InternalsViewer.Query.FullTrace.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Output;

public sealed partial record TraceOutputStructure
{
    internal static void Write(BinaryWriter writer, IReadOnlyList<TraceOutputStructure> structures)
    {
        writer.Write(structures.Count);

        foreach (var structure in structures)
        {
            writer.Write(structure.OutputRow);

            writer.Write(structure.DirectSelectOwner);

            writer.Write(structure.Columns.Count);

            foreach (var column in structure.Columns)
            {
                writer.Write((byte)column.Type);
                writer.Write(column.Value);
                writer.Write(column.Address);
                writer.Write(column.Slot);
                writer.Write(column.Ordinal);
                writer.Write(column.Offset);
                writer.Write(column.Length);
                writer.Write(column.Owner);
            }
        }
    }

    internal static List<TraceOutputStructure> Read(BinaryReader reader)
    {
        var structures = new List<TraceOutputStructure>(reader.ReadInt32());

        for (var index = 0; index < structures.Capacity; index++)
        {
            var outputRow = reader.ReadUInt64();

            var directSelectOwner = reader.ReadUInt64();

            var columns = new TraceOutputSource[reader.ReadInt32()];

            for (var ordinal = 0; ordinal < columns.Length; ordinal++)
            {
                columns[ordinal] = new TraceOutputSource((OutputSourceType)reader.ReadByte(), reader.ReadUInt64(), reader.ReadUInt64())
                {
                    Slot = reader.ReadInt32(),
                    Ordinal = reader.ReadInt32(),
                    Offset = reader.ReadInt32(),
                    Length = reader.ReadInt32(),
                    Owner = reader.ReadUInt64()
                };
            }

            structures.Add(new TraceOutputStructure(outputRow, columns, directSelectOwner));
        }

        return structures;
    }
}
