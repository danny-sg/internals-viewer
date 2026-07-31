using System;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.Controls;

public sealed class PageAddressEventArgs(short fileId, int pageId, ushort? slot = null) : EventArgs
{
    public PageAddressEventArgs(PageAddress pageAddress)
        : this(pageAddress.FileId, pageAddress.PageId, null)
    {
    }

    public short FileId { get; } = fileId;

    public int PageId { get; } = pageId;

    public ushort? Slot { get; init; } = slot;

    public string Tag { get; set; } = string.Empty;

    public PageAddress PageAddress => new(FileId, PageId);
}