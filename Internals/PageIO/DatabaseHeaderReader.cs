using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.PageIo
{
    /// <summary>
    /// Readers header information from a database source
    /// </summary>
    public class DatabaseHeaderReader
    {
        /// <summary>
        /// Loads the header.
        /// </summary>
        /// <param name="pageAddress">The page address.</param>
        /// <returns></returns>
        public static Header LoadHeader(PageAddress pageAddress)
        {
            var header = new Header();

            LoadHeader(LoadPageHeaderOnly(pageAddress), header);

            return header;
        }

        /// <summary>
        /// Loads the header.
        /// </summary>
        /// <param name="headerData">The header data.</param>
        /// <param name="header">The header.</param>
        /// <returns></returns>
        public static bool LoadHeader(IDictionary<string, string> headerData, Header header)
        {
            var parsed = true;

            int slotCount;
            int freeCount;
            int freeData;
            int pageType;
            int level;
            int minLen;
            int indexId;
            long allocationUnitId;
            long objectId;
            long partitionId;
            int reservedCount;
            int xactReservedCount;
            long tornBits;

            parsed &= int.TryParse(headerData["m_slotCnt"], out slotCount);
            parsed &= int.TryParse(headerData["m_freeCnt"], out freeCount);
            parsed &= int.TryParse(headerData["m_freeData"], out freeData);
            parsed &= int.TryParse(headerData["m_type"], out pageType);
            parsed &= int.TryParse(headerData["m_level"], out level);
            parsed &= int.TryParse(headerData["pminlen"], out minLen);
            parsed &= int.TryParse(headerData["Metadata: IndexId"], out indexId);
            parsed &= long.TryParse(headerData["Metadata: AllocUnitId"], out allocationUnitId);
            parsed &= long.TryParse(headerData["Metadata: ObjectId"], out objectId);
            parsed &= long.TryParse(headerData["Metadata: PartitionId"], out partitionId);
            parsed &= int.TryParse(headerData["m_reservedCnt"], out reservedCount);
            parsed &= int.TryParse(headerData["m_xactReserved"], out xactReservedCount);
            parsed &= long.TryParse(headerData["m_tornBits"], out tornBits);

            header.PageAddress = new PageAddress(headerData["m_pageId"]);
            header.PageType = (PageType)pageType;
            header.Lsn = new LogSequenceNumber(headerData["m_lsn"]);
            header.FlagBits = headerData["m_flagBits"];
            header.PreviousPage = new PageAddress(headerData["m_prevPage"]);
            header.NextPage = new PageAddress(headerData["m_nextPage"]);

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

        /// <summary>
        /// Loads the page header only for a given page
        /// </summary>
        /// <param name="pageAddress">The page address.</param>
        /// <returns></returns>
        private static Dictionary<string, string> LoadPageHeaderOnly(PageAddress pageAddress)
        {
            var headerData = new Dictionary<string, string>();

            var pageCommand = string.Format(Properties.Resources.SQL_Page,
                                            InternalsViewerConnection.CurrentConnection().CurrentDatabase.DatabaseId,
                                            pageAddress.FileId,
                                            pageAddress.PageId,
                                            0);

            using (var conn = new SqlConnection(InternalsViewerConnection.CurrentConnection().ConnectionString))
            {
                var cmd = new SqlCommand(pageCommand, conn)
                {
                    CommandType = CommandType.Text
                };

                try
                {
                    conn.Open();
                    var reader = cmd.ExecuteReader();

                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (reader[0].ToString() == "PAGE HEADER:")
                            {
                                headerData.Add(reader[2].ToString(), reader[3].ToString());
                            }
                        }

                        reader.Close();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Print(ex.ToString());
                }

                cmd.Dispose();
            }

            return headerData;
        }
    }
}
