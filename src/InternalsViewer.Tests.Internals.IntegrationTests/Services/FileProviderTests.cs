using InternalsViewer.Internals.Providers.Metadata;
using Xunit.Abstractions;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Services;

public class FileProviderTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [Fact]
    public async Task Can_Load_And_Parse_Metadata()
    {
        var metadata = await GetMetadata();

        var files = FileProvider.GetFiles(metadata);

        Assert.NotEmpty(files);
    }
}