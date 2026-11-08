using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public sealed partial class TraceBufferPool
{
    private const int FirstVersion = 1;

    private const int FormatVersion = 4;

    private const int LayoutVersion = 3;

    private const int PageTypeVersion = 4;

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

        writer.Write(Layouts.Count);

        foreach (var ((page, databaseId), layout) in Layouts)
        {
            writer.Write(page.FileId);

            writer.Write(page.PageId);

            writer.Write(databaseId);

            writer.Write((byte)layout.PageHeader.PageType);

            writer.Write(layout.OffsetTable.Length);

            foreach (var offset in layout.OffsetTable)
            {
                writer.Write(offset);
            }
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

        var layouts = new Dictionary<(PageAddress Page, ushort DatabaseId), PageData>();

        var layoutCount = version >= LayoutVersion ? reader.ReadInt32() : 0;

        for (var index = 0; index < layoutCount; index++)
        {
            var page = new PageAddress(reader.ReadInt16(), reader.ReadInt32());

            var databaseId = reader.ReadUInt16();

            var type = version >= PageTypeVersion ? (PageType)reader.ReadByte() : PageType.None;

            var offsets = new ushort[reader.ReadInt32()];

            for (var slot = 0; slot < offsets.Length; slot++)
            {
                offsets[slot] = reader.ReadUInt16();
            }

            layouts[(page, databaseId)] = CreateLayout(page, offsets, type);
        }

        return new TraceBufferPool(pages, layouts);
    }
}
