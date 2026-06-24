using InternalsViewer.Internals.Metadata.Helpers;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

public class IndexStructureProviderTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringTheory("local")]
    [InlineData(72057594054049792)]
    [InlineData(72057594054115328)]
    //[InlineData(72057594054049792)]
    public async Task Can_Get_IndexStructure(long allocationUnitId)
    {
        var metadata = await GetMetadata();

        var structure = IndexStructureProvider.GetIndexStructure(metadata, allocationUnitId);

        TestOutput.WriteLine(structure.ToDetailString());   

        Assert.NotEmpty(structure.Columns);
    }
}