using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

/// <summary>
/// Parser for Data pages
/// </summary>
public class DataPageParser : PageParser, IPageParser<DataPage>
{
    public PageType[] SupportedPageTypes => new[] { PageType.Data };

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public DataPage Parse(PageData page)
    {
        var dataPage = CopyToPageType<DataPage>(page);

        dataPage.AllocationUnit = dataPage.Database
                                          .AllocationUnits
                                          .FirstOrDefault(a => a.AllocationUnitId == dataPage.PageHeader.AllocationUnitId)
                                  ?? AllocationUnit.Unknown;

        return dataPage;
    }
}