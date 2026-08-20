using System.Text;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

/// <summary>
/// Parser for the Boot page
/// </summary>
public sealed class BootPageParser : PageParser, IPageParser<BootPage>
{
    private const int GuidSize = 16;
    private const int DateTimeSize = 8;
    private const int DatabaseNameSize = 128 * 2;

    private const int CurrentVersionOffset = 100;
    private const int CreatedVersionOffset = 102;
    private const int StatusOffset = 132;
    private const int CreatedDateTimeOffset = 140;
    private const int DatabaseNameOffset = 148;
    private const int DatabaseNameLengthOffset = 404;
    private const int DatabaseIdOffset = 408;
    private const int CompatibilityLevelOffset = 410;
    private const int MaxDatabaseTimestampOffset = 412;
    private const int CheckpointLsnOffset = 444;
    private const int DbccFlagsOffset = 454;
    private const int DirtyPageLsnOffset = 468;
    private const int LastTransactionIdOffset = 480;
    private const int CollationOffset = 488;
    private const int ReleaseStatusOffset = 496;
    private const int FamilyGuidOffset = 504;
    private const int MaxLogSpaceUsedOffset = 520;
    private const int RecoveryForkGuidOffset = 552;
    private const int FirstPageOffset = 612;
    private const int ModifiedDateTimeOffset = 772;
    private const int ResourceDatabaseVersionOffset = 780;
    private const int ServiceBrokerGuidOffset = 788;
    private const int ServiceBrokerOptionsOffset = 804;
    private const int NextAllocationUnitIdOffset = 832;
    private const int LatestVersioningUpgradeLsnOffset = 1548;
    private const int PersistentVersionStoreRowsetIdOffset = 1740;
    private const int PersistentVersionStoreLongTermRowsetIdOffset = 1748;

    public PageType[] SupportedPageTypes => [PageType.Boot];

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public BootPage Parse(PageData page)
    {
        var bootPage = CopyToPageType<BootPage>(page);

        ReadValues(bootPage);

        SetMarkers(bootPage);

        return bootPage;
    }

    private static void ReadValues(BootPage page)
    {
        page.CurrentVersion = BitConverter.ToInt16(page.Data, CurrentVersionOffset);
        page.CreatedVersion = BitConverter.ToInt16(page.Data, CreatedVersionOffset);

        page.DatabaseId = BitConverter.ToInt16(page.Data, DatabaseIdOffset);

        page.DatabaseNameLength = BitConverter.ToInt32(page.Data, DatabaseNameLengthOffset);

        var nameLength = Math.Clamp(page.DatabaseNameLength, 0, DatabaseNameSize);

        page.DatabaseName = Encoding.Unicode.GetString(page.Data, DatabaseNameOffset, nameLength);

        page.CreatedDateTime
            = DateTimeConverters.DecodeDateTime(page.Data.AsSpan()[CreatedDateTimeOffset..(CreatedDateTimeOffset + DateTimeSize)]);

        page.ModifiedDateTime
            = DateTimeConverters.DecodeDateTime(page.Data.AsSpan()[ModifiedDateTimeOffset..(ModifiedDateTimeOffset + DateTimeSize)]);

        page.CompatibilityLevel = BitConverter.ToInt16(page.Data, CompatibilityLevelOffset);

        page.MaxLogSpaceUsed = BitConverter.ToInt64(page.Data, MaxLogSpaceUsedOffset);

        page.MaxDatabaseTimestamp = BitConverter.ToInt64(page.Data, MaxDatabaseTimestampOffset);

        page.Status = BitConverter.ToInt32(page.Data, StatusOffset);

        page.ReleaseStatus = BitConverter.ToInt32(page.Data, ReleaseStatusOffset);

        page.LastTransactionId = BitConverter.ToInt32(page.Data, LastTransactionIdOffset);

        page.DbccFlags = BitConverter.ToInt16(page.Data, DbccFlagsOffset);

        page.NextAllocationUnitId = BitConverter.ToInt64(page.Data, NextAllocationUnitIdOffset);

        page.Collation = BitConverter.ToInt32(page.Data, CollationOffset);

        page.ResourceDatabaseVersion = BitConverter.ToInt32(page.Data, ResourceDatabaseVersionOffset);

        page.ServiceBrokerOptions = BitConverter.ToInt32(page.Data, ServiceBrokerOptionsOffset);

        page.PersistentVersionStoreRowsetId = BitConverter.ToInt64(page.Data, PersistentVersionStoreRowsetIdOffset);

        page.PersistentVersionStoreLongTermRowsetId
            = BitConverter.ToInt64(page.Data, PersistentVersionStoreLongTermRowsetIdOffset);

        page.FamilyGuid = new Guid(page.Data.AsSpan(FamilyGuidOffset, GuidSize));

        page.RecoveryForkGuid = new Guid(page.Data.AsSpan(RecoveryForkGuidOffset, GuidSize));

        page.ServiceBrokerGuid = new Guid(page.Data.AsSpan(ServiceBrokerGuidOffset, GuidSize));

        page.FirstAllocationUnitsPage = PageAddressParser.Parse(
            page.Data.AsSpan(FirstPageOffset, PageAddress.Size));

        page.CheckpointLsn = LogSequenceNumberParser.Parse(
                page.Data[CheckpointLsnOffset..(CheckpointLsnOffset + LogSequenceNumber.Size)]);

        page.DirtyPageLsn = LogSequenceNumberParser.Parse(
                page.Data[DirtyPageLsnOffset..(DirtyPageLsnOffset + LogSequenceNumber.Size)]);

        page.LatestVersioningUpgradeLsn = LogSequenceNumberParser.Parse(
                page.Data[LatestVersioningUpgradeLsnOffset..(LatestVersioningUpgradeLsnOffset + LogSequenceNumber.Size)]);
    }

    private static void SetMarkers(BootPage page)
    {
        page.MarkProperty(nameof(BootPage.CurrentVersion), CurrentVersionOffset, sizeof(short));
        page.MarkProperty(nameof(BootPage.CreatedVersion), CreatedVersionOffset, sizeof(short));
        page.MarkProperty(nameof(BootPage.Status), StatusOffset, sizeof(int));
        page.MarkProperty(nameof(BootPage.CreatedDateTime), CreatedDateTimeOffset, DateTimeSize);
        page.MarkProperty(nameof(BootPage.DatabaseName), DatabaseNameOffset, DatabaseNameSize);
        page.MarkProperty(nameof(BootPage.DatabaseNameLength), DatabaseNameLengthOffset, sizeof(int));
        page.MarkProperty(nameof(BootPage.DatabaseId), DatabaseIdOffset, sizeof(short));
        page.MarkProperty(nameof(BootPage.CompatibilityLevel), CompatibilityLevelOffset, sizeof(short));
        page.MarkProperty(nameof(BootPage.MaxDatabaseTimestamp), MaxDatabaseTimestampOffset, sizeof(long));
        page.MarkProperty(nameof(BootPage.CheckpointLsn), CheckpointLsnOffset, LogSequenceNumber.Size);
        page.MarkProperty(nameof(BootPage.DbccFlags), DbccFlagsOffset, sizeof(short));
        page.MarkProperty(nameof(BootPage.DirtyPageLsn), DirtyPageLsnOffset, LogSequenceNumber.Size);
        page.MarkProperty(nameof(BootPage.LastTransactionId), LastTransactionIdOffset, sizeof(int));
        page.MarkProperty(nameof(BootPage.Collation), CollationOffset, sizeof(int));
        page.MarkProperty(nameof(BootPage.ReleaseStatus), ReleaseStatusOffset, sizeof(int));
        page.MarkProperty(nameof(BootPage.FamilyGuid), FamilyGuidOffset, GuidSize);
        page.MarkProperty(nameof(BootPage.MaxLogSpaceUsed), MaxLogSpaceUsedOffset, sizeof(long));
        page.MarkProperty(nameof(BootPage.RecoveryForkGuid), RecoveryForkGuidOffset, GuidSize);
        page.MarkProperty(nameof(BootPage.FirstAllocationUnitsPage), FirstPageOffset, PageAddress.Size);
        page.MarkProperty(nameof(BootPage.ModifiedDateTime), ModifiedDateTimeOffset, DateTimeSize);
        page.MarkProperty(nameof(BootPage.ResourceDatabaseVersion), ResourceDatabaseVersionOffset, sizeof(int));
        page.MarkProperty(nameof(BootPage.ServiceBrokerGuid), ServiceBrokerGuidOffset, GuidSize);
        page.MarkProperty(nameof(BootPage.ServiceBrokerOptions), ServiceBrokerOptionsOffset, sizeof(int));
        page.MarkProperty(
            nameof(BootPage.NextAllocationUnitId), NextAllocationUnitIdOffset, sizeof(long));
        page.MarkProperty(
            nameof(BootPage.LatestVersioningUpgradeLsn), LatestVersioningUpgradeLsnOffset, LogSequenceNumber.Size);
        page.MarkProperty(
            nameof(BootPage.PersistentVersionStoreRowsetId), PersistentVersionStoreRowsetIdOffset, sizeof(long));
        page.MarkProperty(
            nameof(BootPage.PersistentVersionStoreLongTermRowsetId), PersistentVersionStoreLongTermRowsetIdOffset, sizeof(long));
    }
}
