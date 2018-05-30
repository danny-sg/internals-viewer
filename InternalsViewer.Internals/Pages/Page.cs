using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.PageIo;
using InternalsViewer.Internals.PageIo.Pages;

namespace InternalsViewer.Internals.Pages
{
    /// <summary>
    /// Database Page
    /// </summary>
    public class Page : Markable
    {
        public const int Size = 8192;
        private readonly PageReader reader;

        /// <summary>
        /// Create a Page with a DatabasePageReader
        /// </summary>
        public Page(Database database, PageAddress pageAddress)
        {
            PageAddress = pageAddress;
            Database = database;
            DatabaseId = database.DatabaseId;

            if (pageAddress.FileId == 0)
            {
                return;
            }

            reader = new DatabasePageReader(Database.ConnectionString, PageAddress, DatabaseId);

            LoadPage();
        }

        public Page(string connectionString, string database, PageAddress pageAddress)
        {
            PageAddress = pageAddress;

            DatabaseId = GetDatabaseId(connectionString, database);

            var compatabilityLevel = Database.GetCompatabilityLevel(connectionString, database);
            Database = new Database(connectionString, DatabaseId, database, 1, compatabilityLevel);

            reader = new DatabasePageReader(connectionString, PageAddress, DatabaseId);

            LoadPage();
        }

        /// <summary>
        /// Create a Page with a supplied PageReader
        /// </summary>
        public Page(PageReader reader)
        {
            this.reader = reader;

            LoadPage();

            PageAddress = reader.Header.PageAddress;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Page"/> class.
        /// </summary>
        public Page()
        {
        }

        /// <summary>
        /// Load a page
        /// </summary>
        /// <param name="suppressLoad">Suppress a Page refresh</param>
        protected void LoadPage(bool suppressLoad)
        {
            if (!suppressLoad)
            {
                reader.Load();
                PageData = reader.Data;

                reader.LoadHeader();
                Header = reader.Header;
            }

            if (Header.PageType != PageType.Gam ||
                Header.PageType != PageType.Sgam ||
                Header.PageType != PageType.Pfs)
            {
                DatabaseName = LookupDatabaseName(Database.ConnectionString, DatabaseId);
                Header.PageTypeName = GetPageTypeName(Header.PageType);
                Header.AllocationUnit = LookupAllocationUnit(Header.AllocationUnitId);

                if (Database.CompatibilityLevel > 90)
                {
                    CompressionType = GetPageCompressionType(Database.ConnectionString);
                }

                if (CompressionType == CompressionType.Page)
                {
                    CompressionInformation = new CompressionInformation(this, 96);
                }
            }

            if (Header.SlotCount > 0 && Header.ObjectId > 0)
            {
                LoadOffsetTable(Header.SlotCount);
            }
        }

        /// <summary>
        /// Returns the description of a PageType
        /// </summary>
        public static string GetPageTypeName(PageType pageType)
        {
            switch (pageType)
            {
                case PageType.Data: return "Data";
                case PageType.Index: return "Index";
                case PageType.Lob3:
                case PageType.Lob4: return "LOB (Text/Image)";
                case PageType.Sort: return "Sort";
                case PageType.Gam: return "GAM (Global Allocation Map)";
                case PageType.Sgam: return "SGAM (Shared Global Allocation Map)";
                case PageType.Iam: return "IAM (Index Allocation Map)";
                case PageType.Pfs: return "PFS (Page Free Space)";
                case PageType.Dcm: return "DCM (Differential Changed Map)";
                case PageType.Bcm: return "BCM (Bulk Changed Map)";
                case PageType.Boot: return "Boot Page";
                case PageType.FileHeader: return "File Header Page";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// Refresh the Page
        /// </summary>
        public virtual void Refresh(bool suppressLoad)
        {
            if (PageAddress != PageAddress.Empty)
            {
                if (!suppressLoad)
                {
                    OffsetTable.Clear();
                }

                LoadPage(suppressLoad);
            }
        }

        /// <summary>
        /// Refresh the Page
        /// </summary>
        public virtual void Refresh()
        {
            Refresh(false);
        }

        public bool AllocationStatus(PageType pageType)
        {
            var interval = Database.AllocationInterval;

            var page = new AllocationPage(Database, Allocation.AllocationPageAddress(PageAddress, pageType));

            return page.AllocationMap[(interval / 8) + 1];
        }

        public PfsByte PfsStatus()
        {
            var pfsPage = new PfsPage(Database, Allocation.AllocationPageAddress(PageAddress, PageType.Pfs));

            return pfsPage.PfsBytes[PageAddress.PageId % Database.PfsInterval];
        }

        /// <summary>
        /// Lookups the name of the database.
        /// </summary>
        private static string LookupDatabaseName(string connectionString, int databaseId)
        {
            string databaseName;

            databaseName = (string)DataAccess.GetScalar(connectionString,
                                                        "master",
                                                        Properties.Resources.SQL_Database,
                                                        CommandType.Text,
                                                        new SqlParameter[1] { new SqlParameter("database_id", databaseId) });

            return databaseName;
        }

        /// <summary>
        /// Gets the type of the page compression.
        /// </summary>
        /// <returns></returns>
        private CompressionType GetPageCompressionType(string connectionString)
        {
            if (Header != null)
            {
                return (CompressionType)(DataAccess.GetScalar(connectionString,
                                                              DatabaseName,
                                                              Properties.Resources.SQL_Compression,
                                                              CommandType.Text,
                                                              new SqlParameter[1]
                                                                           {
                                                                           new SqlParameter("partition_id",
                                                                                            Header.PartitionId)
                                                                           }) ?? CompressionType.None);
            }
            else
            {
                return CompressionType.None;
            }
        }

        /// <summary>
        /// Load the Page without a data refresh
        /// </summary>
        private void LoadPage()
        {
            LoadPage(false);
        }

        /// <summary>
        /// Load the offset table with a given slot count from the page data
        /// </summary>
        private void LoadOffsetTable(int slotCount)
        {
            for (var i = 2; i <= (slotCount * 2); i += 2)
            {
                OffsetTable.Add(BitConverter.ToUInt16(PageData, PageData.Length - i));
            }
        }

        /// <summary>
        /// Lookups the allocation unit.
        /// </summary>
        /// <param name="allocationUnitId">The allocation unit id.</param>
        /// <returns></returns>
        private string LookupAllocationUnit(long allocationUnitId)
        {
            string allocationUnitName;

            var sqlCommand = Properties.Resources.SQL_Allocation_Unit;

            if (DatabaseName == null)
            {
                allocationUnitName = Header.AllocationUnit;
            }
            else
            {
                allocationUnitName = (string)DataAccess.GetScalar(Database.ConnectionString,
                                                                  DatabaseName,
                                                                  sqlCommand,
                                                                   CommandType.Text,
                                                                   new SqlParameter[1]
                                                                       {
                                                                           new SqlParameter("allocation_unit_id",
                                                                                           allocationUnitId)
                                                                       });
            }

            return allocationUnitName;
        }

        private static short GetDatabaseId(string connectionString, string database)
        {
            short databaseId;

            databaseId = (short)DataAccess.GetScalar(connectionString,
                                                     "master",
                                                     Properties.Resources.SQL_DatabaseId,
                                                     CommandType.Text,
                                                       new SqlParameter[1]
                                                                         {
                                                                             new SqlParameter("DatabaseName", database)
                                                                         });
            return databaseId;
        }

        #region Properties

        /// <summary>
        /// Gets or sets the type of page compression (2008+).
        /// </summary>
        /// <value>The type of the compression.</value>
        public CompressionType CompressionType { get; set; }

        /// <summary>
        /// Gets the name of the database.
        /// </summary>
        /// <value>The name of the database.</value>
        public string DatabaseName { get; private set; }

        /// <summary>
        /// Gets the database.
        /// </summary>
        /// <value>The database.</value>
        public Database Database { get; }

        /// <summary>
        /// Gets or sets the page address.
        /// </summary>
        /// <value>The page address.</value>
        public PageAddress PageAddress { get; set; }

        /// <summary>
        /// Gets or sets the database id.
        /// </summary>
        /// <value>The database id.</value>
        public int DatabaseId { get; set; }

        /// <summary>
        /// Gets or sets the page data.
        /// </summary>
        /// <value>The page data.</value>
        public byte[] PageData { get; set; }

        /// <summary>
        /// Gets or sets the header.
        /// </summary>
        /// <value>The header.</value>
        public Header Header { get; set; }

        /// <summary>
        /// Gets the offset table.
        /// </summary>
        /// <value>The offset table.</value>
        public List<ushort> OffsetTable { get; } = new List<ushort>();

        public CompressionInformation CompressionInformation { get; set; }

        #endregion
    }
}
