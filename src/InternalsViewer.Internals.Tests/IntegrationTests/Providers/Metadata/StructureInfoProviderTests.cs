using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

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
    public async Task Can_Get_Compression_Type()
    {
        var provider = GetProvider();

        var compressionType = await provider.GetCompressionType(72057594047954944);

        Assert.Equal(CompressionType.None, compressionType);
    }
}