using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

/// <summary>
/// Parser for LOB (Large Object Data) pages
/// </summary>
public sealed class LobPageParser : PageParser, IPageParser<LobPage>
{
    public PageType[] SupportedPageTypes => [PageType.Lob3, PageType.Lob4];

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public LobPage Parse(PageData page)
    {
        var lobPage = CopyToPageType<LobPage>(page);

        var allocationUnit = lobPage.Database
                                    .AllocationUnits
                                    .TryGetValue(lobPage.PageHeader.AllocationUnitId,
                                        out var value) ? value : AllocationUnit.Unknown;

        lobPage.AllocationUnit = allocationUnit;

        return lobPage;
    }
}