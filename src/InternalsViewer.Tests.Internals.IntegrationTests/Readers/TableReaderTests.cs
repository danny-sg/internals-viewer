using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Metadata.Internals.Tables;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Tests.Internals.IntegrationTests.TestHelpers;
using Xunit.Abstractions;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Readers;
public class TableReaderTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [Fact]
    public async Task Can_Read_AllocationUnits_Table()
    {
        var service = ServiceHelper.CreatePageService(TestOutput);

        var dataReader = new TableReader(service);

        var database = new DatabaseDetail { Name = "AdventureWorks2022" };

        var tableStructure = InternalAllocationUnitStructure.GetStructure(72057594040549376);

        var records = await dataReader.Read(database, new InternalsViewer.Internals.Engine.Address.PageAddress(1, 20), tableStructure);

        var result = records.Select(InternalAllocationUnitLoader.Load).ToList();

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Can_Read_Objects_Table()
    {
        var service = ServiceHelper.CreatePageService(TestOutput);

        var dataReader = new TableReader(service);

        var database = new DatabaseDetail { Name = "AdventureWorks2022" };

        var tableStructure = InternalObjectStructure.GetStructure(72057594040549376);

        var records = await dataReader.Read(database, new InternalsViewer.Internals.Engine.Address.PageAddress(1, 273), tableStructure);

        var result = records.Select(InternalObjectLoader.Load).ToList();

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Can_Read_Columns_Table()
    {
        var service = ServiceHelper.CreatePageService(TestOutput);

        var dataReader = new TableReader(service);

        var database = new DatabaseDetail { Name = "AdventureWorks2022" };

        var tableStructure = InternalColumnStructure.GetStructure(72057594040549376);

        var records = await dataReader.Read(database, new InternalsViewer.Internals.Engine.Address.PageAddress(1, 19), tableStructure);

        var result = records.Select(InternalColumnLoader.Load).ToList();

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Can_Read_RowSet_Table()
    {
        var service = ServiceHelper.CreatePageService(TestOutput);

        var dataReader = new TableReader(service);

        var database = new DatabaseDetail { Name = "AdventureWorks2022" };

        var tableStructure = InternalRowSetStructure.GetStructure(72057594040549376);

        var records = await dataReader.Read(database, new InternalsViewer.Internals.Engine.Address.PageAddress(1, 19), tableStructure);

        var result = records.Select(InternalRowSetLoader.Load).ToList();

        Assert.NotEmpty(result);
    }
}
