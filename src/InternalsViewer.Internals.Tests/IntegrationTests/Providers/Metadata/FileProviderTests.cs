using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

public class FileProviderTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringFact("local")]
    public async Task Can_Load_And_Parse_Metadata()
    {
        var metadata = await GetMetadata();

        var files = FileProvider.GetFiles(metadata);

        Assert.NotEmpty(files);
    }
}