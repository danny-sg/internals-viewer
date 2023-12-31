using InternalsViewer.Internals.Metadata.Helpers;
using InternalsViewer.Internals.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

public class IndexStructureProviderTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [Theory]
    //[InlineData(72057594054049792)]
    //[InlineData(72057594054115328)]
    [InlineData(72057594054049792)]
    public async Task Can_Get_IndexStructure(long allocationUnitId)
    {
        var metadata = await GetMetadata();

        var structure = IndexStructureProvider.GetIndexStructure(metadata, allocationUnitId);

        TestOutput.WriteLine(structure.ToDetailString());   

        Assert.NotEmpty(structure.Columns);
    }
}