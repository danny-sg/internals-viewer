namespace InternalsViewer.Query.FullTrace.Memory.History;

internal sealed partial class MemoryHistoryAccessTable
{
    internal static MemoryHistoryAccessTable Read(BinaryReader reader, int cacheSlots)
    {
        var count = reader.ReadInt32();

        var chunks = new byte[reader.ReadInt32()][];

        if (count < 0 || chunks.Length != (count + ChunkMask) >> ChunkShift)
        {
            throw new InvalidDataException("The memory history accesses do not match their chunk count");
        }

        var dataLengths = new int[chunks.Length];

        for (var index = 0; index < chunks.Length; index++)
        {
            dataLengths[index] = reader.ReadInt32();

            chunks[index] = reader.ReadBytes(reader.ReadInt32());
        }

        return new MemoryHistoryAccessTable(chunks, dataLengths, count, cacheSlots);
    }

    internal void Write(BinaryWriter writer)
    {
        writer.Write(Count);

        writer.Write(Chunks.Length);

        for (var index = 0; index < Chunks.Length; index++)
        {
            writer.Write(DataLengths[index]);

            writer.Write(Chunks[index].Length);

            writer.Write(Chunks[index]);
        }
    }
}
