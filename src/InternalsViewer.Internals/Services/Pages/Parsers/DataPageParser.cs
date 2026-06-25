using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Services.Loaders.Compression;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

/// <summary>
/// Parser for Data pages
/// </summary>
public sealed class DataPageParser(CompressionInfoLoader compressionInfoLoader) : PageParser, IPageParser<DataPage>
{
    public PageType[] SupportedPageTypes => [PageType.Data];

    private CompressionInfoLoader CompressionInfoLoader { get; } = compressionInfoLoader;

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public DataPage Parse(PageData page)
    {
        var dataPage = CopyToPageType<DataPage>(page);

        var allocationUnit = dataPage.Database
                                     .AllocationUnits
                                     .TryGetValue(dataPage.PageHeader.AllocationUnitId, 
                                                  out var value) ? value : AllocationUnit.Unknown;

        dataPage.AllocationUnit = allocationUnit;

        if (dataPage.AllocationUnit.CompressionType == CompressionType.Page && dataPage.IsPageCompressed)
        {
            dataPage.CompressionInfo = CompressionInfoLoader.Load(dataPage, CompressionInfo.SlotOffset);
        }

        return dataPage;
    }
}