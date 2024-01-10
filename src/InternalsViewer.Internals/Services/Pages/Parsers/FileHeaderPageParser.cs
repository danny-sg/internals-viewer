using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

public class FileHeaderPageParser: PageParser, IPageParser<FileHeaderPage>
{
    public PageType[] SupportedPageTypes => new[] { PageType.FileHeader };

    public FileHeaderPage Parse(PageData page)
    {
        var fileHeaderPage = CopyToPageType<FileHeaderPage>(page);

        return fileHeaderPage;
    }

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }
}