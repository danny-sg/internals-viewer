using InternalsViewer.Connection.BackupFile.Reader;

namespace InternalsViewer.Connection.BackupFile.Format.Streams;

internal sealed class DataStream
{
    private const long MaxMaterializedStreamLength = 0x10000;

    public StreamHeader Header { get; }

    public long DataPosition { get; }

    public byte[] Data { get; }

    public DataStream(BackupReader reader)
    {
        Header = new StreamHeader(reader);

        DataPosition = reader.BaseStream.Position;

        if (Header.StreamLength <= MaxMaterializedStreamLength)
        {
            Data = reader.ReadBytes((int)Header.StreamLength);
        }
        else
        {
            Data = [];

            reader.BaseStream.Seek((long)Header.StreamLength, SeekOrigin.Current);
        }

        var boundaryOffset = (4 - reader.BaseStream.Position % 4) % 4;

        reader.BaseStream.Seek(boundaryOffset, SeekOrigin.Current);
    }
}
