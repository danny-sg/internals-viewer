using System.Buffers.Binary;
using System.Threading;
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
public sealed class PageLoader : IPageLoader
{
    public async Task<PageData> Load(DatabaseSource database,
                                     PageAddress pageAddress,
                                     CancellationToken cancellationToken,
                                     bool isMarkEnabled = true)
    {
        var data = await database.Connection.PageReader.Read(database.Name, pageAddress, cancellationToken);

        return BuildPageData(database, pageAddress, data, isMarkEnabled);
    }

    /// <summary>
    /// Loads a page into a provided buffer
    /// </summary>
    /// <remarks>
    /// Buffer scenarios are when pages are loaded on a transitory basis for index traversal etc. so markers are switched off
    /// </remarks>
    public async Task<PageData> LoadInto(DatabaseSource database,
                                         PageAddress pageAddress,
                                         byte[] buffer,
                                         CancellationToken cancellationToken)
    {
        await database.Connection.PageReader.ReadInto(database.Name, pageAddress, buffer, cancellationToken);

        return BuildPageData(database, pageAddress, buffer, false);
    }

    private static PageData BuildPageData(DatabaseSource database,
                                          PageAddress pageAddress,
                                          byte[] data,
                                          bool isMarkEnabled)
    {
        var header = PageHeaderParser.Parse(data, isMarkEnabled);

        return new PageData
        {
            Database = database,
            PageAddress = pageAddress,
            Data = data,
            PageHeader = header,
            OffsetTable = LoadOffsetTable(data, header.SlotCount),
            IsMarkEnabled = isMarkEnabled
        };
    }

    /// <summary>
    /// Load the offset table with a given slot count from the page data
    /// </summary>
    private static ushort[] LoadOffsetTable(byte[] data, int slotCount)
    {
        var offsetTable = new ushort[slotCount];

        ReadOnlySpan<byte> span = data;

        var offset = PageData.Size - 2;

        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            offsetTable[slotIndex] = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2));

            offset -= 2;
        }

        return offsetTable;
    }
}