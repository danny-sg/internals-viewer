using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Services.Loaders.Compression;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

/// <summary>
/// Parser for Data pages
/// </summary>
public class DataPageParser(CompressionInfoLoader compressionInfoLoader) : PageParser, IPageParser<DataPage>
{
    private CompressionInfoLoader CompressionInfoLoader { get; } = compressionInfoLoader;

    public PageType[] SupportedPageTypes => new[] { PageType.Data };

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public DataPage Parse(PageData page)
    {
        var dataPage = CopyToPageType<DataPage>(page);

        dataPage.AllocationUnit = dataPage.Database
                                          .AllocationUnits
                                          .FirstOrDefault(a => a.AllocationUnitId == dataPage.PageHeader.AllocationUnitId)
                                  ?? AllocationUnit.Unknown;

        if(dataPage.AllocationUnit.CompressionType == CompressionType.Page && dataPage.IsPageCompressed)
        {
            dataPage.CompressionInfo = CompressionInfoLoader.Load(dataPage, CompressionInfo.SlotOffset);
        }

        return dataPage;
    }
}