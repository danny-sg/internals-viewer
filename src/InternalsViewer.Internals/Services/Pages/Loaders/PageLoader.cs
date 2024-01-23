using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Services.Pages.Parsers;

namespace InternalsViewer.Internals.Services.Pages.Loaders;

/// <summary>
/// Responsible for loading Page Data
/// </summary>
/// <remarks>
/// Page Data is the raw data from the page plus a parsed paged header. These elements are common to all pages. Once the data has been 
/// loaded further parsing can be performed for specific page types.
/// </remarks>
public class PageLoader : IPageLoader
{
    public async Task<PageData> Load(DatabaseSource database, PageAddress pageAddress)
    {
        var data = await database.Connection.PageReader.Read(database.Name, pageAddress);

        var header = PageHeaderParser.Parse(data);

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