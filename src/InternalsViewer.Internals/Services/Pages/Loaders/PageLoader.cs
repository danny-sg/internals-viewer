using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Loaders;

/// <summary>
/// Responsible for loading Page Data
/// </summary>
/// <remarks>
/// Page Data is the raw data from the page plus a parsed paged header. These elements are common to all pages. Once the data has been 
/// loaded further parsing can be performed for specific page types.
/// </remarks>
public class PageLoader(IPageReader reader) : IPageLoader
{
    public IPageReader Reader { get; } = reader;

    public async Task<PageData> Load(DatabaseDetail database, PageAddress pageAddress)
    {
        var data = await Reader.Read(database.Name, pageAddress);

        var header = PageHeaderLoader.Read(data);

        var page = new PageData
        {
            Database = database,
            PageAddress = pageAddress,
            Data = data,
            PageHeader = header,
            OffsetTable = LoadOffsetTable(data, header.SlotCount)
        };

        return page;
    }

    /// <summary>
    /// Load the offset table with a given slot count from the page data
    /// </summary>
    public static List<ushort> LoadOffsetTable(byte[] data, int slotCount)
    {
        var offsetTable = new List<ushort>();

        for (var i = 2; i <= slotCount * 2; i += 2)
        {
            offsetTable.Add(BitConverter.ToUInt16(data, data.Length - i));
        }

        return offsetTable;
    }
}