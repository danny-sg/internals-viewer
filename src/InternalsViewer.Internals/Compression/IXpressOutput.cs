namespace InternalsViewer.Internals.Compression;

/// <summary>
/// Sink an Xpress Huffman decoder writes to
/// </summary>
/// <remarks>
/// Matches reference output already produced, so the sink has to retain enough history to resolve them.
/// </remarks>
public interface IXpressOutput
{
    long Length { get; }

    void WriteLiteral(byte value);

    void WriteMatch(int offset, int length);
}
