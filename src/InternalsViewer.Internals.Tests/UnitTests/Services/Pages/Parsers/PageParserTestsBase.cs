using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Services.Pages.Loaders;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Pages.Parsers;

public abstract class PageParserTestsBase(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    protected DatabaseSource Database { get; set; }

    protected async Task<PageData> GetPageData(PageAddress pageAddress)
    {
        var filePath = "./UnitTests/Test Data/Test Pages/";

        var reader = new FilePageReader(filePath);

        var pageLoader = new PageLoader();

        return await pageLoader.Load(Database, pageAddress);
    }
}