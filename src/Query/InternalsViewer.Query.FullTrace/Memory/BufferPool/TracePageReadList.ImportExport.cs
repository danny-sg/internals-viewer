namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public sealed partial class TracePageReadList
{
    private const int FormatVersion = 1;

    internal static bool IsSupported(BinaryReader reader) => -reader.ReadInt32() == FormatVersion;

    internal void Write(BinaryWriter writer)
    {
        writer.Write(-FormatVersion);

        writer.Write(Reads.Length);

        foreach (var read in Reads)
        {
            writer.Write(read.Position.Sequence);

            writer.Write(read.Position.Steps);

            writer.Write(read.Thread);

            writer.Write(read.Address);

            writer.Write(read.Length);
        }
    }

    internal static TracePageReadList Read(BinaryReader reader)
    {
        if (-reader.ReadInt32() != FormatVersion)
        {
            throw new InvalidDataException("The page reads were exported in an unknown format and must be re-recorded");
        }

        var reads = new TracePageRead[reader.ReadInt32()];

        for (var index = 0; index < reads.Length; index++)
        {
            reads[index] = new TracePageRead(new TracePosition(reader.ReadUInt64(), reader.ReadUInt64()),
                                             reader.ReadUInt32(),
                                             reader.ReadUInt64(),
                                             reader.ReadUInt16());
        }

        return new TracePageReadList(reads);
    }
}
