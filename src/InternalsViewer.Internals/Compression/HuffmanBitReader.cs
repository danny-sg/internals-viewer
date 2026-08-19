namespace InternalsViewer.Internals.Compression;

/// <summary>
/// Bit reader over a payload written as sixteen bit little endian words consumed most significant bit first
/// </summary>
public sealed class HuffmanBitReader
{
    private ReadOnlyMemory<byte> _payload;

    private uint _mask;

    private int _bits;

    private int _position;

    /// <summary>
    /// Bytes the reader pulls in before any bit is consumed, being the two words the mask is filled from
    /// </summary>
    private const int PrefetchBytes = 4;

    /// <summary>
    /// Byte cursor the word reader has reached, which runs ahead of the consumed bits
    /// </summary>
    public int BytePosition => _position;

    /// <summary>
    /// Bits consumed from the payload, which is the cursor behind the prefetched words rather than the byte one
    /// </summary>
    public int BitPosition => ((_position - PrefetchBytes) * 8) + (16 - _bits);

    public void Reset(ReadOnlyMemory<byte> value, int byteOffset = 0)
    {
        _payload = value;

        _position = byteOffset;

        _mask = (uint)ReadWord() << 16;
        _mask |= (uint)ReadWord();

        _bits = 16;
    }

    /// <summary>
    /// Restarts the reader at an arbitrary bit offset within the current payload
    /// </summary>
    public void SeekBits(int bitOffset)
    {
        Reset(_payload, bitOffset / 16 * 2);

        Skip(bitOffset % 16);
    }

    public int Peek(int count) => (int)(_mask >> (32 - count));

    public void Skip(int count)
    {
        _mask <<= count;
        _bits -= count;

        if (_bits < 0)
        {
            _mask |= (uint)ReadWord() << -_bits;
            _bits += 16;
        }
    }

    public int ReadBits(int count)
    {
        if (count == 0)
        {
            return 0;
        }

        var value = Peek(count);

        Skip(count);

        return value;
    }

    public byte ReadRawByte()
    {
        var span = _payload.Span;

        var value = _position < span.Length ? span[_position] : (byte)0;

        _position++;

        return value;
    }

    public int ReadRawUInt16() => ReadRawByte() | (ReadRawByte() << 8);

    private int ReadWord()
    {
        var span = _payload.Span;

        var value = 0;

        if (_position + 1 < span.Length)
        {
            value = span[_position] | (span[_position + 1] << 8);
        }
        else if (_position < span.Length)
        {
            value = span[_position];
        }

        _position += 2;

        return value;
    }
}
