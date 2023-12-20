using InternalsViewer.Internals.Providers.Metadata;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Services;

public class FileProviderTests: ProviderTestBase
{
    [Fact]
    public async Task Can_Load_And_Parse_Metadata()
    {
        var metadata = await GetMetadata();

        var files = FileProvider.GetFiles(metadata);

        Assert.NotEmpty(files);
    }
}