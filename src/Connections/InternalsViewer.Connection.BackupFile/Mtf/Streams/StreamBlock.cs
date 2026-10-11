using InternalsViewer.Connection.BackupFile.Mtf.Blocks;

namespace InternalsViewer.Connection.BackupFile.Mtf.Streams;

internal sealed class StreamBlock
{
    public long StartPosition { get; set; }

    public BlockType BlockType { get; set; }

    public uint Attributes { get; set; }

    public ushort Length { get; set; }

    public StreamBlock(BinaryReader reader) => ReadHeader(reader);

    private void ReadHeader(BinaryReader reader)
    {
        StartPosition = reader.BaseStream.Position;

        BlockType = (BlockType) reader.ReadUInt32();

        Attributes = reader.ReadUInt32();
        
        Length = reader.ReadUInt16();
    }
}
