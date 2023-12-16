using InternalsViewer.Internals.Engine.Address;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Readers.Headers;
using System;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using System.Linq;

namespace InternalsViewer.Internals.Services.Loaders;

/// <summary>
/// Service responsible for loading Page information
/// </summary>
public class PageService(IPageReader reader,
                         ICompressionInfoService compressionInfoService) : IPageService
{
    public IPageReader Reader { get; } = reader;

    public ICompressionInfoService CompressionInfoService { get; } = compressionInfoService;

    public async Task<T> Load<T>(Database database, PageAddress pageAddress) where T : Page, new()
    {
        var page = new T
        {
            Database = database,
            PageAddress = pageAddress
        };

        var data = await Reader.Read(database.Name, pageAddress);

        page.PageData = data;

        LoadHeader(data, page, database);

        LoadOffsetTable(page);

        await LoadCompressionInfo(page);

        return page;
    }

    /// <summary>
    /// Load the page header from the page data and lookup the allocation unit object
    /// </summary>
    private static void LoadHeader(byte[] data, Page page, Database database)
    {
        var header = HeaderReader.Read(data);

        page.PageHeader = header;

        var allocationUnit = database.AllocationUnits.FirstOrDefault(a => a.AllocationUnitId == header.AllocationUnitId);

        page.AllocationUnit = allocationUnit ?? AllocationUnit.Unknown;
    }

    /// <summary>
    /// Load the offset table with a given slot count from the page data
    /// </summary>
    public static void LoadOffsetTable(Page page)
    {
        var slotCount = page.PageHeader.SlotCount;

        for (var i = 2; i <= slotCount * 2; i += 2)
        {
            page.OffsetTable.Add(BitConverter.ToUInt16(page.PageData, page.PageData.Length - i));
        }
    }

    /// <summary>
    /// Get compression info (if required)
    /// </summary>
    private async Task LoadCompressionInfo(Page page)
    {
        //page.CompressionInfo = await CompressionInfoService.GetCompressionInfo(page);
    }
}