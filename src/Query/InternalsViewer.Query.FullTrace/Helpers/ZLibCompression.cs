using System.Buffers;
using System.IO.Compression;

namespace InternalsViewer.Query.FullTrace.Helpers;

internal static class ZLibCompression
{
    private static ThreadLocal<ZLibDecoder> Decoders { get; } = new(() => new ZLibDecoder());

    public static byte[] Compress(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second = default)
    {
        using var encoder = new ZLibEncoder();

        var output = ArrayPool<byte>.Shared.Rent((int)ZLibEncoder.GetMaxCompressedLength(first.Length + second.Length));

        try
        {
            var written = Compress(encoder, first, output, isFinalBlock: second.IsEmpty);

            if (!second.IsEmpty)
            {
                written += Compress(encoder, second, output.AsSpan(written), isFinalBlock: true);
            }

            return [.. output.AsSpan(0, written)];
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(output);
        }
    }

    public static Reader Open(ReadOnlySpan<byte> source) => new(source, Decoders.Value!);

    private static int Compress(ZLibEncoder encoder, ReadOnlySpan<byte> source, Span<byte> destination, bool isFinalBlock)
    {
        var status = encoder.Compress(source, destination, out _, out var written, isFinalBlock);

        if (status != OperationStatus.Done)
        {
            throw new InvalidOperationException($"zlib compression stopped with {status}");
        }

        return written;
    }

    internal ref struct Reader
    {
        public Reader(ReadOnlySpan<byte> source, ZLibDecoder decoder)
        {
            decoder.Reset();

            Source = source;

            Decoder = decoder;
        }

        private ReadOnlySpan<byte> Source { get; set; }

        private ZLibDecoder Decoder { get; }

        public int Read(Span<byte> destination)
        {
            var status = Decoder.Decompress(Source, destination, out var consumed, out var written);

            if (status == OperationStatus.InvalidData)
            {
                throw new InvalidDataException("The compressed data is not valid zlib data");
            }

            Source = Source[consumed..];

            return written;
        }

        public void ReadExactly(Span<byte> destination)
        {
            if (Read(destination) != destination.Length)
            {
                throw new EndOfStreamException("The compressed data ended before the expected length");
            }
        }
    }
}
