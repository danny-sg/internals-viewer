using System.IO;
namespace InternalsViewer.Internals.Compression;

/// <summary>
/// Xpress Huffman sink for a payload that expands into a single known size buffer
/// </summary>
public sealed class XpressBufferOutput(int size) : IXpressOutput
{
    private readonly byte[] buffer = new byte[size];

    private int position;

    public long Length => position;

    public ReadOnlyMemory<byte> Buffer => buffer.AsMemory(0, position);

    public void WriteLiteral(byte value) => buffer[position++] = value;

    public void WriteMatch(int offset, int length)
    {
        var source = position - offset;

        if (source < 0)
        {
            throw new InvalidDataException($"Match offset {offset} reaches before the start of the output.");
        }

        for (var i = 0; i < length; i++)
        {
            buffer[position++] = buffer[source + i];
        }
    }
}
