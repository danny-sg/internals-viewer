using System.Text;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

/// <summary>
/// Parser for Boot pages
/// </summary>
public class BootPageParser : PageParser, IPageParser<BootPage>
{
    private const int CurrentVersionOffset = 100;
    private const int CreatedVersionOffset = 102;
    private const int StatusOffset = 132;
    private const int CreatedDateTimeOffset = 140;
    private const int DatabaseIdOffset = 408;
    private const int DatabaseNameOffset = 148;
    private const int CompatibilityLevelOffset = 410;
    private const int MaxLogSpaceUsed = 520;
    private const int CheckpointLsnOffset = 520;
    private const int CollationOffset = 488;
    private const int FirstPageOffset = 612;
    private const int NextAllocationUnitIdOffset = 832;

    public PageType[] SupportedPageTypes => new[] { PageType.Boot };

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public BootPage Parse(PageData page)
    {
        var bootPage = CopyToPageType<BootPage>(page);

        ReadValues(bootPage);

        SetHeaderMarkers(bootPage);

        return bootPage;
    }

    private static void ReadValues(BootPage page)
    {
        page.CurrentVersion = BitConverter.ToInt16(page.Data, CurrentVersionOffset);
        page.CreatedVersion = BitConverter.ToInt16(page.Data, CreatedVersionOffset);

        page.DatabaseId = BitConverter.ToInt16(page.Data, DatabaseIdOffset);
        page.DatabaseName = Encoding.Unicode.GetString(page.Data[DatabaseNameOffset..(DatabaseNameOffset + 128 * 2)]).TrimEnd();

        page.CreatedDateTime = DateTimeConverters.DecodeDateTime(page.Data[CreatedDateTimeOffset..(CreatedDateTimeOffset + 8)]);
        page.CompatibilityLevel = BitConverter.ToInt16(page.Data, CompatibilityLevelOffset);

        page.MaxLogSpaceUsed = BitConverter.ToInt64(page.Data, MaxLogSpaceUsed);

        page.Status = BitConverter.ToInt32(page.Data, StatusOffset);

        page.NextAllocationUnitId = BitConverter.ToInt64(page.Data, NextAllocationUnitIdOffset);

        page.Collation = BitConverter.ToInt32(page.Data, CollationOffset);

        page.FirstAllocationUnitsPage = PageAddressParser.Parse(page.Data[FirstPageOffset..(FirstPageOffset + PageAddress.Size)]);

        page.CheckpointLsn = LogSequenceNumberParser.Parse(
                page.Data[CheckpointLsnOffset..(CheckpointLsnOffset + LogSequenceNumber.Size)]);
    }

    private void SetHeaderMarkers(BootPage page)
    {
    }
}