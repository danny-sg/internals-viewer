using System;
using System.Collections.Generic;
using System.Data;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Readers.Pages;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Pages;

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

        var compatibilityLevel = Database.GetCompatabilityLevel(connectionString, database);

        Database = new Database(connectionString, DatabaseId, database, 1, compatibilityLevel);

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
        return pageType switch
        {
            PageType.Data => "Data",
            PageType.Index => "Index",
            PageType.Lob3 => "LOB (Text/Image)",
            PageType.Lob4 => "LOB (Text/Image)",
            PageType.Sort => "Sort",
            PageType.Gam => "GAM (Global Allocation Map)",
            PageType.Sgam => "SGAM (Shared Global Allocation Map)",
            PageType.Iam => "IAM (Index Allocation Map)",
            PageType.Pfs => "PFS (Page Free Space)",
            PageType.Dcm => "DCM (Differential Changed Map)",
            PageType.Bcm => "BCM (Bulk Changed Map)",
            PageType.Boot => "Boot Page",
            PageType.FileHeader => "File Header Page",
            PageType.None => string.Empty,
            _ => string.Empty
        };
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
        const int interval = Database.AllocationInterval;

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
        var databaseName = (string)DataAccess.GetScalar(connectionString,
            "master",
            Properties.Resources.SQL_Database,
            CommandType.Text,
            new SqlParameter[1] { new("database_id", databaseId) });

        return databaseName;
    }

    /// <summary>
    /// Gets the type of the page compression.
    /// </summary>
    private CompressionType GetPageCompressionType(string connectionString)
    {
        if (Header != null)
        {
            return (CompressionType)(DataAccess.GetScalar(connectionString,
                DatabaseName,
                Properties.Resources.SQL_Compression,
                CommandType.Text,
                new SqlParameter[]
                {
                    new("partition_id", Header.PartitionId)
                }) ?? CompressionType.None);
        }

        return CompressionType.None;
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
                new SqlParameter[]
                {
                    new("allocation_unit_id", allocationUnitId)
                });
        }

        return allocationUnitName;
    }

    private static short GetDatabaseId(string connectionString, string database)
    {
        var databaseId = (short)DataAccess.GetScalar(connectionString,
            "master",
            Properties.Resources.SQL_DatabaseId,
            CommandType.Text,
            new SqlParameter[]
            {
                new("DatabaseName", database)
            });
        return databaseId;
    }

    /// <summary>
    /// Gets or sets the type of page compression (2008+).
    /// </summary>
    public CompressionType CompressionType { get; set; }

    /// <summary>
    /// Gets the name of the database.
    /// </summary>
    public string DatabaseName { get; private set; }

    /// <summary>
    /// Gets the database.
    /// </summary>
    public Database Database { get; }

    /// <summary>
    /// Gets or sets the page address.
    /// </summary>
    public PageAddress PageAddress { get; set; }

    /// <summary>
    /// Gets or sets the database id.
    /// </summary>
    public int DatabaseId { get; set; }

    /// <summary>
    /// Gets or sets the page data.
    /// </summary>
    public byte[] PageData { get; set; }

    /// <summary>
    /// Gets or sets the header.
    /// </summary>
    public Header Header { get; set; }

    /// <summary>
    /// Gets the offset table.
    /// </summary>
    public List<ushort> OffsetTable { get; } = new();

    public CompressionInformation CompressionInformation { get; set; }
}