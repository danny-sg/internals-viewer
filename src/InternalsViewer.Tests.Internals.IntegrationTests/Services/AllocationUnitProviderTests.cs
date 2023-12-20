using InternalsViewer.Internals.Providers.Metadata;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Services;

public class AllocationUnitProviderTests: ProviderTestBase
{
    [Fact]
    public async Task Can_Get_AllocationUnits()
    {
        var metadata = await GetMetadata();

        var allocationUnits = AllocationUnitProvider.GetAllocationUnits(metadata);

        Assert.NotEmpty(allocationUnits);
    }
}