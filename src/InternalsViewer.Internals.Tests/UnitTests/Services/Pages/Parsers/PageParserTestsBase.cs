using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Services.Pages.Loaders;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Pages.Parsers;

public abstract class PageParserTestsBase(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string FilePath = "./UnitTests/Test Data/Test Pages/";

    protected async Task<PageData> GetPageData(PageAddress pageAddress)
    {
        var reader = new FilePageReader(FilePath);
        var connection = new FileConnectionType(reader, "TestDatabase");
        var database = new DatabaseSource(connection) { Name = "TestDatabase" };

        var pageLoader = new PageLoader();

        return await pageLoader.Load(database, pageAddress);
    }
}