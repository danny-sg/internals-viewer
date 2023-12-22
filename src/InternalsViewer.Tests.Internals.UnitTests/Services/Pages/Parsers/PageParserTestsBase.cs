using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Services.Pages.Loaders;
using InternalsViewer.Tests.Internals.UnitTests.TestHelpers.TestReaders;
using Xunit.Abstractions;

namespace InternalsViewer.Tests.Internals.UnitTests.Services.Pages.Parsers;

public abstract class PageParserTestsBase(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    protected DatabaseDetail Database { get; set; } = new() { Name = "TestDatabase" };

    protected async Task<PageData> GetPageData(PageAddress pageAddress)
    {
        var filePath = "./Test Data/Test Pages/";

        var reader = new FilePageReader(filePath);

        var pageLoader = new PageLoader(reader);

        return await pageLoader.Load(Database, pageAddress);
    }
}