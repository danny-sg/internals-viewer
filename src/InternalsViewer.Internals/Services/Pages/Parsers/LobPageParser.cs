using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

public class LobPageParser : PageParser, IPageParser<LobPage>
{
    public PageType[] SupportedPageTypes => new[] { PageType.Lob3, PageType.Lob4 };

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public LobPage Parse(PageData page)
    {
        var lobPage = CopyToPageType<LobPage>(page);

        return lobPage;
    }
}