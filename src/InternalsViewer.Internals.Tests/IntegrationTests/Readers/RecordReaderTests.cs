using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Metadata.Internals.Tables;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Readers;

public class RecordReaderTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    protected IConnectionType Connection => FileConnectionFactory.Create(c => c.Filename = "./IntegrationTests/Test Data/TestDatabase.mdf");


    [Fact]
    public async Task Can_Read_AllocationUnits_Table()
    {
        var service = ServiceHelper.CreatePageService(TestOutput);
      
        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(testOutput), service, loader);

        var database = new DatabaseSource(Connection) { Name = "TestDatabase" };

        var tableStructure = InternalAllocationUnitStructure.GetStructure(72057594040549376);

        var records = await dataReader.Read(database, new PageAddress(1, 20), tableStructure);

        var result = records.Select(InternalAllocationUnitLoader.Load).ToList();

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Can_Read_Objects_Table()
    {
        var service = ServiceHelper.CreatePageService(TestOutput);

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(testOutput), service, loader);

        var database = new DatabaseSource(Connection) { Name = "TestDatabase" };

        var tableStructure = InternalObjectStructure.GetStructure(72057594040549376);

        var records = await dataReader.Read(database, new PageAddress(1, 273), tableStructure);

        var result = records.Select(InternalObjectLoader.Load).ToList();

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Can_Read_Columns_Table()
    {
        var service = ServiceHelper.CreatePageService(TestOutput);

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(testOutput), service, loader);

        var database = new DatabaseSource(Connection) { Name = "TestDatabase" };

        var tableStructure = InternalColumnStructure.GetStructure(72057594040549376);

        var records = await dataReader.Read(database, new PageAddress(1, 19), tableStructure);

        var result = records.Select(InternalColumnLoader.Load).ToList();

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Can_Read_RowSet_Table()
    {
        var service = ServiceHelper.CreatePageService(TestOutput);

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(testOutput), service, loader);

        var database = new DatabaseSource(Connection) { Name = "TestDatabase" };

        var tableStructure = InternalRowSetStructure.GetStructure(72057594040549376);

        var records = await dataReader.Read(database, new PageAddress(1, 19), tableStructure);

        var result = records.Select(InternalRowSetLoader.Load).ToList();

        Assert.NotEmpty(result);
    }
}
