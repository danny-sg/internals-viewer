using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

[Trait("Category", "Integration")]
[Trait("Area", "Columnstore")]
public sealed class ColumnstoreAllocationProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [Theory]
    [InlineData("SegDeletes")]
    [InlineData("SegDelta")]
    [InlineData("Sales")]
    public async Task Probe_Allocation_Units(string tableName)
    {
        var database = await LoadDatabase();

        var units = database.AllocationUnits.Values.Where(a => a.TableName == tableName).ToList();

        TestOutput.WriteLine($"{tableName}: {units.Count} allocation unit(s)");

        foreach (var unit in units)
        {
            TestOutput.WriteLine($"  au {unit.AllocationUnitId} type {unit.AllocationUnitType} "
                                 + $"partition {unit.PartitionId} objectId {unit.ObjectId} indexId {unit.IndexId} "
                                 + $"indexType {unit.IndexType} first {unit.FirstPage} root {unit.RootPage} "
                                 + $"iam {unit.FirstIamPage} used {unit.UsedPages} total {unit.TotalPages}");
        }

        foreach (var partitionId in units.Select(u => u.PartitionId).Distinct())
        {
            if (database.Metadata.Rowsets.TryGetValue(partitionId, out var rowset))
            {
                TestOutput.WriteLine($"  rowset {partitionId} ownerType {rowset.OwnerType} indexId {rowset.IndexId} "
                                     + $"partitionNumber {rowset.PartitionNumber} rows {rowset.RowCount} status {rowset.Status}");
            }
        }

        Assert.NotEmpty(units);
    }
}
