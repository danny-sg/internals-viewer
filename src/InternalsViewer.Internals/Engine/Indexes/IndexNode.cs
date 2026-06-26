using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages.Enums;

namespace InternalsViewer.Internals.Engine.Indexes;

public sealed class IndexNode(PageAddress pageAddress)
{
    public PageAddress PageAddress { get; set; } = pageAddress;

    public PageType PageType { get; set; }

    public List<PageAddress> Parents { get; set; } = new();

    public List<PageAddress> Children { get; set; } = new();

    public PageAddress NextPage { get; set; }

    public PageAddress PreviousPage { get; set; }

    public byte Level { get; set; }

    public ushort Ordinal { get; set; }
    
    public byte IndexLevel { get; set; }
}