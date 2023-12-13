using System.Collections.Generic;

namespace InternalsViewer.Tests.Internals.UnitTests.Services.Loaders;

public class TestHeader
{
    public static Dictionary<string, string> HeaderValues { get; set; } = new()
    {
        {"m_slotCnt", "1"},
        {"m_freeCnt", "2"},
        {"m_freeData", "3"},
        {"m_type", "8"},
        {"m_level", "0"},
        {"pminlen", "1"},
        {"Metadata: IndexId", "1"},
        {"Metadata: AllocUnitId", "6488064"},
        {"Metadata: ObjectId", "99"},
        {"Metadata: PartitionId", "0"},
        {"m_reservedCnt", "1"},
        {"m_xactReserved", "2"},
        {"m_tornBits", "-273531820"},
        {"m_pageId", "(1:2)"},
        {"m_lsn", "(53:29745:7)"},
        {"m_flagBits", "0x200"},
        {"m_prevPage", "(0:0)"},
        {"m_nextPage", "(0:0)"}
    };
}