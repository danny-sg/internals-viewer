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
}