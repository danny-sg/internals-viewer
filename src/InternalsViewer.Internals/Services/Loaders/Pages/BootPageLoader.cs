using System.Text;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.Services.Loaders;

namespace InternalsViewer.Internals.Services.Loaders.Pages;

/// <summary>
/// Service responsible for loading the boot page
/// </summary>
/// <param name="pageLoader"></param>
public class BootPageLoader(IPageLoader pageLoader) : IBootPageLoader
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

    public IPageLoader PageLoader { get; } = pageLoader;

    public async Task<BootPage> GetBootPage(DatabaseDetail databaseDetail)
    {
        var page = await PageLoader.Load<BootPage>(databaseDetail, BootPage.BootPageAddress);

        ReadValues(page);

        SetHeaderMarkers(page);

        return page;
    }

    private static void ReadValues(BootPage page)
    {
        page.CurrentVersion = BitConverter.ToInt16(page.PageData, CurrentVersionOffset);
        page.CreatedVersion = BitConverter.ToInt16(page.PageData, CreatedVersionOffset);
        page.DatabaseId = BitConverter.ToInt16(page.PageData, DatabaseIdOffset);
        page.DatabaseName = Encoding.Unicode.GetString(page.PageData[DatabaseNameOffset..(DatabaseNameOffset + 128 * 2)]).TrimEnd();
        page.CreatedDateTime = DateTimeConverters.DecodeDateTime(page.PageData[CreatedDateTimeOffset..(CreatedDateTimeOffset + 8)]);
        page.CompatibilityLevel = BitConverter.ToInt16(page.PageData, CompatibilityLevelOffset);
        page.MaxLogSpaceUsed = BitConverter.ToInt64(page.PageData, MaxLogSpaceUsed);

        page.Status = BitConverter.ToInt32(page.PageData, StatusOffset);

        page.NextAllocationUnitId = BitConverter.ToInt64(page.PageData, NextAllocationUnitIdOffset);

        page.Collation = BitConverter.ToInt32(page.PageData, CollationOffset);

        page.FirstAllocationUnitsPage = PageAddressParser.Parse(page.PageData[FirstPageOffset..(FirstPageOffset + PageAddress.Size)]);

        page.CheckpointLsn = LogSequenceNumberParser.Parse(
                page.PageData[CheckpointLsnOffset..(CheckpointLsnOffset + LogSequenceNumber.Size)]);
    }

    private void SetHeaderMarkers(BootPage page)
    {

    }
}