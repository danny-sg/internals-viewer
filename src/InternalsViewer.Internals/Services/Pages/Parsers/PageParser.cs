using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

public abstract class PageParser
{
    protected T CopyToPageType<T>(PageData pageData) where T : Page, new()
    {
        var page = new T
        {
            PageHeader = pageData.PageHeader,
            Database = pageData.Database,
            Data = pageData.Data,
            PageAddress = pageData.PageAddress,
            OffsetTable = pageData.OffsetTable,
        };

        return page;
    }
}