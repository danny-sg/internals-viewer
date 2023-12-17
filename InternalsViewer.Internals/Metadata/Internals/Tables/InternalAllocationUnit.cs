using System.Data;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Generators;

namespace InternalsViewer.Internals.Metadata.Internals.Tables;

/// <summary>
/// SQL Server Allocation Units definitions table - sys.sysallocunits    
/// </summary>
[InternalsMetadata]
public record InternalAllocationUnit
{
    public static PageAddress Location => new(1, 20);

    [InternalsMetadataColumn("auid", 1, SqlDbType.BigInt, 8, 4, 1)]
    public long AllocationUnitId { get; set; }

    [InternalsMetadataColumn("type", 2, SqlDbType.TinyInt, 1, 12, 2)]
    public byte Type { get; set; }

    [InternalsMetadataColumn("ownerid", 3, SqlDbType.BigInt, 8, 13, 3)]
    public long PartitionId { get; set; }

    [InternalsMetadataColumn("status", 4, SqlDbType.Int, 4, 21, 4)]
    public int Status { get; set; }

    [InternalsMetadataColumn("fgid", 5, SqlDbType.SmallInt, 2, 25, 5)]
    public short FileGroupId { get; set; }

    [InternalsMetadataColumn("pgfirst", 6, SqlDbType.Binary, 6, 27, 6)]
    public byte[]? FirstPage { get; set; }

    [InternalsMetadataColumn("pgroot", 7, SqlDbType.Binary, 6, 33, 7)]
    public byte[]? RootPage { get; set; }

    [InternalsMetadataColumn("pgfirstiam", 8, SqlDbType.Binary, 6, 39, 8)]
    public byte[]? FirstIamPage { get; set; }

    [InternalsMetadataColumn("pcused", 9, SqlDbType.BigInt, 8, 45, 9)]
    public long UsedPages { get; set; }

    [InternalsMetadataColumn("pcdata", 10, SqlDbType.BigInt, 8, 53, 10)]
    public long DataPages { get; set; }

    [InternalsMetadataColumn("pcreserved", 11, SqlDbType.BigInt, 8, 61, 11)]
    public long TotalPages { get; set; }
}