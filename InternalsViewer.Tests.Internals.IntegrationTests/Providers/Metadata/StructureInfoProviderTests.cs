using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Tests.Internals.IntegrationTests.Helpers;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Providers.Metadata;

public class StructureInfoProviderTests
{
    private StructureInfoProvider GetProvider()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection { ConnectionString = connectionString, DatabaseName = "AdventureWorks2022" };

        var provider = new StructureInfoProvider(connection);

        return provider;
    }

    [Fact]
    public async Task Can_Get_Index_Structure()
    {
        var provider = GetProvider();

        var indexStructure = await provider.GetIndexStructure(72057594055622656);

        Assert.True(indexStructure.Columns.Count > 0);
    }

    [Fact]
    public async Task Can_Get_Table_Structure()
    {
        var provider = GetProvider();

        var tableStructure = await provider.GetTableStructure(72057594055622656);

        Assert.True(tableStructure.Columns.Count > 0);
    }

    [Fact]
    public async Task Can_Get_Compression_Type()
    {
        var provider = GetProvider();

        var compressionType = await provider.GetCompressionType(72057594047954944);

        Assert.Equal(CompressionType.None, compressionType);
    }
    [Fact]
    public async Task Can_Get_Entry_Points()
    {
        var provider = GetProvider();

        var entryPoints = await provider.GetEntryPoints("Person.Address", "PK_Address_AddressID");

        Assert.True(entryPoints.Count > 0);

        var entryPoint = entryPoints.First();

        // Pages taken from the current AdventureWorks2022 database
        Assert.Equal(new PageAddress(1, 12401), entryPoint.FirstIam);

        Assert.Equal(new PageAddress(1, 13328), entryPoint.FirstPage);

        Assert.Equal(new PageAddress(1, 13512), entryPoint.RootPage);
    }

    [Fact]
    public async Task Can_Get_Name()
    {
        var provider = GetProvider();

        var name = await provider.GetName(72057594055622656);

        Assert.Equal("Person.Address", name);
    }

    [Fact]
    public async Task Can_Get_Structure_Type()
    {
        var provider = GetProvider();

        var structureType = await provider.GetStructureType("Person.Address");

        Assert.Equal(StructureType.BTree, structureType);
    }
}