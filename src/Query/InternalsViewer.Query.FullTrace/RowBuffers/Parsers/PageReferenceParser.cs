using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Services.Pages.Parsers;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

internal static class PageReferenceParser
{
    private const byte PageHeaderVersion = 1;

    public static DeferredPageValue? Parse(ITraceReplay replay, ulong pointer, ulong length)
    {
        var offset = pointer & (PageData.Size - 1);

        if (pointer < ColumnValueParser.SmallestAddress || offset < (ulong)PageHeader.Size || length > PageData.Size - offset)
        {
            return null;
        }

        var page = pointer - offset;

        Span<byte> lsn = stackalloc byte[LogSequenceNumber.Size];

        if (ReadPageId(replay, page) is not { } pageId || replay.Read(page + PageHeaderParser.LsnOffset, lsn) != lsn.Length)
        {
            return null;
        }

        return new DeferredPageValue(pageId, (int)offset, LogSequenceNumberParser.Parse(lsn));
    }

    public static PageAddress? ReadPageId(ITraceReplay replay, ulong page)
    {
        Span<byte> pageId = stackalloc byte[PageAddress.Size];

        if (replay.Read(page + PageHeaderParser.PageAddressOffset, pageId) != pageId.Length
            || (replay.TryRead(page, out byte version) && version != PageHeaderVersion))
        {
            return null;
        }

        return ParsePageId(pageId);
    }

    public static PageAddress? ParsePageId(ReadOnlySpan<byte> pageId)
        => PageAddressParser.Parse(pageId) is { FileId: > 0 } address ? address : null;
}
