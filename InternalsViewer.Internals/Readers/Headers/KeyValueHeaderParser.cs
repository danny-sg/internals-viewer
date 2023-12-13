using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Readers.Headers;

public class KeyValueHeaderParser
{
    /// <summary>
    /// Loads the header.
    /// </summary>
    public static bool TryParse(Dictionary<string, string> values, out Header result)
    {
        result = new Header();

        var parsed = true;

        parsed &= int.TryParse(GetValue("m_slotCnt"), out var slotCount);
        parsed &= int.TryParse(GetValue("m_freeCnt"), out var freeCount);
        parsed &= int.TryParse(GetValue("m_freeData"), out var freeData);
        parsed &= int.TryParse(GetValue("m_type"), out var pageType);
        parsed &= int.TryParse(GetValue("m_level"), out var level);
        parsed &= int.TryParse(GetValue("pminlen"), out var minLen);
        parsed &= int.TryParse(GetValue("Metadata: IndexId"), out var indexId);
        parsed &= long.TryParse(GetValue("Metadata: AllocUnitId"), out var allocationUnitId);
        parsed &= long.TryParse(GetValue("Metadata: ObjectId"), out var objectId);
        parsed &= long.TryParse(GetValue("Metadata: PartitionId"), out var partitionId);
        parsed &= int.TryParse(GetValue("m_reservedCnt"), out var reservedCount);
        parsed &= int.TryParse(GetValue("m_xactReserved"), out var xactReservedCount);
        parsed &= long.TryParse(GetValue("m_tornBits"), out var tornBits);

        result.PageAddress = new PageAddress(GetValue("m_pageId"));
        result.PageType = (PageType)pageType;
        result.Lsn = new LogSequenceNumber(GetValue("m_lsn"));
        result.FlagBits = GetValue("m_flagBits");
        result.PreviousPage = new PageAddress(GetValue("m_prevPage"));
        result.NextPage = new PageAddress(GetValue("m_nextPage"));

        result.SlotCount = slotCount;
        result.FreeCount = freeCount;
        result.FreeData = freeData;
        result.Level = level;
        result.MinLen = minLen;
        result.IndexId = indexId;
        result.AllocationUnitId = allocationUnitId;
        result.ObjectId = objectId;
        result.PartitionId = partitionId;
        result.ReservedCount = reservedCount;
        result.XactReservedCount = xactReservedCount;
        result.TornBits = tornBits;

        return parsed;

        string GetValue(string key) => values[key];
    }
}