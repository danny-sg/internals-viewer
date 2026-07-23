using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

public class AllocationUnitProviderTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringFact("local")]
    public async Task Can_Get_AllocationUnits()
    {
        var metadata = await GetMetadata();

        var allocationUnits = AllocationUnitProvider.GetAllocationUnits(metadata);

        Assert.NotEmpty(allocationUnits);
    }

    [RequiresConnectionStringTheory("local")]
    [InlineData(72057594060734464)]
    public async Task Can_Get_AllocationUnit(long allocationUnitId)
    {
        var metadata = await GetMetadata();

        var source = metadata.AllocationUnits[allocationUnitId];
        var result = AllocationUnitProvider.GetAllocationUnit(metadata, source);

        Assert.NotNull(result);
    }
}