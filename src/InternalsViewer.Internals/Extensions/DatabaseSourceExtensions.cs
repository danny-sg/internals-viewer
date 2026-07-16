using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;

namespace InternalsViewer.Internals.Extensions;

public static class DatabaseSourceExtensions
{
    public static AllocationUnit? FindPageAllocationUnit(this DatabaseSource databaseSource, PageAddress page)
    {
        if (databaseSource.Pfs.TryGetValue(page.FileId, out var pfs) && !pfs.GetPageStatus(page.PageId).IsAllocated)
        {
            return null;
        }

        var extent = page.PageId / 8;

        return databaseSource.AllocationUnits
                             .Values
                             .FirstOrDefault(u =>
                                             u.IamChain.IsExtentAllocated(extent, page.FileId, false) ||
                                             u.IamChain.SinglePageSlots.Contains(page)
                                             || (u.FirstPage == page && u.IndexType == IndexType.Clustered)
                                             || u.FirstIamPage == page
                                             || (u.RootPage == page && u.IndexType == IndexType.Clustered));
    }

    public static AllocationUnit? FindObjectIdAllocationUnit(this DatabaseSource databaseSource, int objectId)
    {
        return databaseSource.AllocationUnits
                             .Values
                             .FirstOrDefault(f => f.ObjectId == objectId 
                                                  && f.AllocationUnitType == AllocationUnitType.InRowData);
    }

    /// <summary>
    /// Find allocation unit from a HoBT Id
    /// </summary>
    /// <remarks>
    /// HoBT Id is sys.partitions.hobt_id - internally sys.sysrowsets.rowsetid
    ///
    /// Maps to Allocation Unit Partition Id/Container Id
    /// </remarks>
    public static AllocationUnit? FindHobtIdAllocationUnit(this DatabaseSource databaseSource, long hobtId)
    {
        return databaseSource.AllocationUnits
                             .Values
                             .FirstOrDefault(f => f.PartitionId == hobtId
                                                  && f.AllocationUnitType == AllocationUnitType.InRowData);
    }
}