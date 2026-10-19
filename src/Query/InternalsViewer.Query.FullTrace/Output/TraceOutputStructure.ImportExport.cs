namespace InternalsViewer.Query.FullTrace.Output;

internal sealed partial record TraceOutputStructure
{
    private const int FormatVersion = 3;

    internal static void Write(BinaryWriter writer, IReadOnlyList<TraceOutputStructure> structures)
    {
        writer.Write(-FormatVersion);

        writer.Write(structures.Count);

        foreach (var structure in structures)
        {
            writer.Write(structure.OutputRow);

            writer.Write(structure.DirectSelectOwner);

            writer.Write(structure.Owners.Count);

            foreach (var owner in structure.Owners)
            {
                writer.Write(owner);
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

            var directSelectOwner = reader.ReadUInt64();

            var owners = new ulong[reader.ReadInt32()];

            for (var ordinal = 0; ordinal < owners.Length; ordinal++)
            {
                owners[ordinal] = reader.ReadUInt64();
            }

            structures.Add(new TraceOutputStructure(outputRow, owners, directSelectOwner));
        }

        return structures;
    }
}
