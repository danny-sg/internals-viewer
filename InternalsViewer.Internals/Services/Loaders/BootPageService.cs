using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Services.Loaders;

public class BootPageService(IPageService pageService) : IBootPageService
{
    private const int CurrentVersionOffset = 100;
    private const int CreatedVersionOffset = 102;
    private const int StatusOffset = 132;
    private const int CreatedDateTimeOffset = 140;
    private const int DatabaseIdOffset = 408;
    private const int DatabaseNameOffset = 148;
    private const int CompatibilityLevelOffset = 410;
    private const int MaxLogSpaceUsed= 520;
    private const int CheckpointLsnOffset = 520;
    private const int CollationOffset = 488;
    private const int FirstPageOffset = 612;
    private const int NextAllocationUnitIdOffset = 832;

    public IPageService PageService { get; } = pageService;

    public async Task<BootPage> GetBootPage(Database database)
    {
        var page = await PageService.Load<BootPage>(database, BootPage.BootPageAddress);

        page.CheckpointLsn = GetCheckpointLsn(page.PageData);

        GetDatabaseInformation(page);

        return page;
    }

    /// <summary>
    /// Loads the checkpoint LSN directly from the page data.
    /// </summary>
    private static LogSequenceNumber GetCheckpointLsn(byte[] pageData)
    {
        var checkpointLsnValue = new byte[LogSequenceNumber.Size];

        Array.Copy(pageData, CheckpointLsnOffset, checkpointLsnValue, 0, LogSequenceNumber.Size);

        return LogSequenceNumberParser.Parse(checkpointLsnValue);
    }

    private static void GetDatabaseInformation(BootPage page)
    {
        page.CurrentVersion = BitConverter.ToInt16(page.PageData, CurrentVersionOffset);
        page.CreatedVersion = BitConverter.ToInt16(page.PageData, CreatedVersionOffset);
        page.DatabaseId = BitConverter.ToInt16(page.PageData, DatabaseIdOffset);
        page.DatabaseName = Encoding.Unicode.GetString(page.PageData[DatabaseNameOffset..(DatabaseNameOffset + (128 * 2))]).TrimEnd();
        page.CreatedDateTime = DataConverter.DecodeDateTime(page.PageData[CreatedDateTimeOffset..(CreatedDateTimeOffset + 8)]);
        page.CompatibilityLevel = BitConverter.ToInt16(page.PageData, CompatibilityLevelOffset);
        page.MaxLogSpaceUsed = BitConverter.ToInt64(page.PageData, MaxLogSpaceUsed);

        page.Status = BitConverter.ToInt32(page.PageData, StatusOffset);

        page.NextAllocationUnitId = BitConverter.ToInt64(page.PageData, NextAllocationUnitIdOffset);

        page.Collation = BitConverter.ToInt32(page.PageData, CollationOffset);
        
        page.FirstAllocationUnitsPage = PageAddressParser.Parse(page.PageData[FirstPageOffset..(FirstPageOffset + PageAddress.Size)]);
    }
}