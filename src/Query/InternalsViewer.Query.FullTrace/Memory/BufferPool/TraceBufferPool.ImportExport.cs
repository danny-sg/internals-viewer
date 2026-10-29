using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public sealed partial class TraceBufferPool
{
    private const int FirstVersion = 1;

    private const int FormatVersion = 2;

    internal static bool IsSupported(BinaryReader reader) => -reader.ReadInt32() is >= FirstVersion and <= FormatVersion;

    internal void Write(BinaryWriter writer)
    {
        writer.Write(-FormatVersion);

        writer.Write(Pages.Length);

        foreach (var page in Pages)
        {
            writer.Write(page.Frame);

            writer.Write(page.Page.FileId);

            writer.Write(page.Page.PageId);

            writer.Write(page.DatabaseId);

            writer.Write(page.Position.Sequence);

            writer.Write(page.Position.Steps);

            writer.Write(page.Buffer);
        }
    }

    internal static TraceBufferPool Read(BinaryReader reader)
    {
        var version = -reader.ReadInt32();

        if (version is < FirstVersion or > FormatVersion)
        {
            throw new InvalidDataException("The buffer pool pages were exported in an unknown format and must be re-recorded");
        }

        var pages = new TraceBufferPage[reader.ReadInt32()];

        for (var index = 0; index < pages.Length; index++)
        {
            pages[index] = new TraceBufferPage(reader.ReadUInt64(),
                                               new PageAddress(reader.ReadInt16(), reader.ReadInt32()),
                                               reader.ReadUInt16(),
                                               new TracePosition(reader.ReadUInt64(), reader.ReadUInt64()),
                                               version > FirstVersion ? reader.ReadUInt64() : 0);
        }

        return new TraceBufferPool(pages);
    }
}
