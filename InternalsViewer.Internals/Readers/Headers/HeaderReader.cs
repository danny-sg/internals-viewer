using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Readers.Headers
{
    public abstract class HeaderReader
    {
        /// <summary>
        /// Reads a header.
        /// </summary>
        public bool LoadHeader(Header header)
        {
            var parsed = ParseHeader(header);

            return parsed;
        }

        /// <summary>
        /// Loads the header.
        /// </summary>
        protected bool ParseHeader(Header header)
        {
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

            header.PageAddress = new PageAddress(GetValue("m_pageId"));
            header.PageType = (PageType)pageType;
            header.Lsn = new LogSequenceNumber(GetValue("m_lsn"));
            header.FlagBits = GetValue("m_flagBits");
            header.PreviousPage = new PageAddress(GetValue("m_prevPage"));
            header.NextPage = new PageAddress(GetValue("m_nextPage"));

            header.SlotCount = slotCount;
            header.FreeCount = freeCount;
            header.FreeData = freeData;
            header.Level = level;
            header.MinLen = minLen;
            header.IndexId = indexId;
            header.AllocationUnitId = allocationUnitId;
            header.ObjectId = objectId;
            header.PartitionId = partitionId;
            header.ReservedCount = reservedCount;
            header.XactReservedCount = xactReservedCount;
            header.TornBits = tornBits;

            return parsed;
        }

        protected abstract string GetValue(string key);
    }
}