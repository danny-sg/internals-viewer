using InternalsViewer.Internals.Engine.Address;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Readers.Headers;
using System;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;

namespace InternalsViewer.Internals.Services.Loaders;

public class PageService(IDatabaseInfoProvider databaseInfoProvider,
                         IStructureInfoProvider structureInfoProvider,
                         IPageReader reader,
                         ICompressionInfoService compressionInfoService) : IPageService
{
    public IPageReader Reader { get; } = reader;

    public IDatabaseInfoProvider DatabaseInfoProvider { get; } = databaseInfoProvider;

    public IStructureInfoProvider StructureInfoProvider { get; } = structureInfoProvider;

    public ICompressionInfoService CompressionInfoService { get; } = compressionInfoService;

    public async Task<T> Load<T>(Database database, PageAddress pageAddress) where T : Page, new()
    {
        var page = new T();

        page.Database = database;

        page.PageAddress = pageAddress;

        var data = await Reader.Read(database.Name, pageAddress);

        page.PageData = data.Data;

        LoadHeader(data, page);

        LoadOffsetTable(page);

        await LoadCompressionInfo(page);

        return page;
    }

    private static void LoadHeader(PageData data, Page page)
    {
        _ = KeyValueHeaderParser.TryParse(data.HeaderValues, out var header);

        page.Header = header;
    }

    /// <summary>
    /// Load the offset table with a given slot count from the page data
    /// </summary>
    public static void LoadOffsetTable(Page page)
    {
        var slotCount = page.Header.SlotCount;

        for (var i = 2; i <= slotCount * 2; i += 2)
        {
            page.OffsetTable.Add(BitConverter.ToUInt16(page.PageData, page.PageData.Length - i));
        }
    }

    private async Task LoadCompressionInfo(Page page)
    {
        var compressionType = await StructureInfoProvider.GetCompressionType(page.Header.PartitionId);

        page.CompressionType = compressionType;

        if (compressionType == CompressionType.Page)
        {
            page.CompressionInfo = CompressionInfoService.Load(page);
        }
    }
}