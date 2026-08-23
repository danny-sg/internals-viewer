using System.IO;

namespace InternalsViewer.Internals.Compression;

/// <summary>
/// Xpress Huffman sink for a payload that expands into a single known size buffer
/// </summary>
public sealed class XpressBufferOutput(int size) : IXpressOutput
{
    private readonly byte[] _buffer = new byte[size];

    private int _position;

    public long Length => _position;

    public ReadOnlyMemory<byte> Buffer => _buffer.AsMemory(0, _position);

    public void WriteLiteral(byte value) => _buffer[_position++] = value;

    public void WriteMatch(int offset, int length)
    {
        var source = _position - offset;

        if (source < 0)
        {
            throw new InvalidDataException($"Match offset {offset} reaches before the start of the output.");
        }

        for (var i = 0; i < length; i++)
        {
            _buffer[_position++] = _buffer[source + i];
        }
    }
}
