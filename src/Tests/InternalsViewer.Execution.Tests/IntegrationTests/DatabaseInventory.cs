using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests;

public class DatabaseInventory(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    [RequiresFileFact(MdfPath)]
    public async Task List_Allocation_Units()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var database = await serviceHost.GetService<IDatabaseService>()
                                        .LoadAsync("TestDatabase", connection, CancellationToken.None);

        foreach (var unit in database.AllocationUnits.Values
                                     .Where(u => !u.IsSystem)
                                     .OrderBy(u => u.TableName)
                                     .ThenBy(u => u.IndexId))
        {
            TestOutput.WriteLine($"{unit.SchemaName}.{unit.TableName,-18} index={unit.IndexName,-38} "
                                 + $"id={unit.IndexId,-3} type={unit.AllocationUnitType,-12} "
                                 + $"root={unit.RootPage} firstIam={unit.FirstIamPage} pages={unit.UsedPages}");
        }
    }
}
