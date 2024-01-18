using InternalsViewer.Internals.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

public class AllocationUnitProviderTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [Fact]
    public async Task Can_Get_AllocationUnits()
    {
        var metadata = await GetMetadata();

        var allocationUnits = AllocationUnitProvider.GetAllocationUnits(metadata);

        Assert.NotEmpty(allocationUnits);
    }

    [Theory]
    [InlineData(72057594060734464)]
    public async Task Can_Get_AllocationUnit(long allocationUnitId)
    {
        var metadata = await GetMetadata();

        var source = metadata.AllocationUnits.First(a=> a.AllocationUnitId == allocationUnitId);
        var result = AllocationUnitProvider.GetAllocationUnit(metadata, source);

        Assert.NotNull(result);
    }
}