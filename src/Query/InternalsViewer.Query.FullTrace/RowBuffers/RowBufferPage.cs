using System.Buffers.Binary;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Parsers;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed record RowBufferPage(PageAddress Page, int Offset, LogSequenceNumber Lsn)
{
    internal const int Size = sizeof(ushort) + PageAddress.Size + LogSequenceNumber.Size;

    public override string ToString() => $"Page {Page} Offset {Offset}";

    internal static RowBufferPage Parse(ReadOnlySpan<byte> data)
        => new(PageAddressParser.Parse(data, sizeof(ushort)),
               BinaryPrimitives.ReadUInt16LittleEndian(data),
               LogSequenceNumberParser.Parse(data[(sizeof(ushort) + PageAddress.Size)..]));
}
