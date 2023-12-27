using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

public class EmptyPageParser : PageParser, IPageParser<EmptyPage>
{
    public PageType[] SupportedPageTypes => new[] { PageType.None };

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public EmptyPage Parse(PageData page)
    {
        var emptyPage = CopyToPageType<EmptyPage>(page);

        return emptyPage;
    }
}