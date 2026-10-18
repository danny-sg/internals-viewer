namespace InternalsViewer.Query.FullTrace.Output;

public sealed partial record TraceOutputStructure
{
    private const int FormatVersion = 2;

    internal static void Write(BinaryWriter writer, IReadOnlyList<TraceOutputStructure> structures)
    {
        writer.Write(-FormatVersion);

        writer.Write(structures.Count);

        foreach (var structure in structures)
        {
            writer.Write(structure.OutputRow);

            writer.Write(structure.DirectSelectBuffer);

            writer.Write(structure.Columns.Count);

            foreach (var column in structure.Columns)
            {
                TraceOutputSource.Write(writer, column);
            }
        }
    }

    internal static List<TraceOutputStructure> Read(BinaryReader reader)
    {
        if (reader.ReadInt32() != -FormatVersion)
        {
            throw new InvalidDataException("The output structures were exported in an older format and must be re-recorded");
        }

        var structures = new List<TraceOutputStructure>(reader.ReadInt32());

        for (var index = 0; index < structures.Capacity; index++)
        {
            var outputRow = reader.ReadUInt64();

            var directSelectBuffer = reader.ReadUInt64();

            var columns = new TraceOutputSource[reader.ReadInt32()];

            for (var ordinal = 0; ordinal < columns.Length; ordinal++)
            {
                columns[ordinal] = TraceOutputSource.Read(reader);
            }

            structures.Add(new TraceOutputStructure(outputRow, columns, directSelectBuffer));
        }

        return structures;
    }
}
