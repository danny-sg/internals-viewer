using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

public class IndexPageParser : PageParser, IPageParser<IndexPage>
{
    public PageType[] SupportedPageTypes => new[] { PageType.Index };

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public IndexPage Parse(PageData page)
    {
        var indexPage = CopyToPageType<IndexPage>(page);

        indexPage.AllocationUnit = indexPage.Database
                                       .AllocationUnits
                                       .FirstOrDefault(a => a.AllocationUnitId == indexPage.PageHeader.AllocationUnitId)
                                   ?? AllocationUnit.Unknown;

        return indexPage;
    }
}