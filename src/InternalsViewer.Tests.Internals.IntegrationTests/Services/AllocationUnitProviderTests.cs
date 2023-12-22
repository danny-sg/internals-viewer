using InternalsViewer.Internals.Providers.Metadata;
using Xunit.Abstractions;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Services;

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