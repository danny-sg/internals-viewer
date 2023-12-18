using System;
using System.Data;
using InternalsViewer.Internals.Generators;

// ReSharper disable StringLiteralTypo

namespace InternalsViewer.Internals.Metadata.Internals.Tables;

/// <summary>
/// sys.sysrowsets
/// 
/// Contains a row for each partition rowset for an index or a heap.
/// </summary>
[InternalsMetadata]
public record InternalRowSet
{
    [InternalsMetadataColumn("rowsetid", 1, SqlDbType.BigInt, 8, 4, 1)]
    public long PartitionId { get; set; }

    [InternalsMetadataColumn("ownertype", 2, SqlDbType.TinyInt, 1, 12, 2)]
    public byte OwnerType { get; set; }

    [InternalsMetadataColumn("idmajor", 3, SqlDbType.Int, 4, 13, 3)]
    public int ObjectId { get; set; }

    [InternalsMetadataColumn("idminor", 4, SqlDbType.Int, 4, 17, 4)]
    public int IndexId { get; set; }

    [InternalsMetadataColumn("numpart", 5, SqlDbType.Int, 4, 21, 5)]
    public int PartitionNumber{ get; set; }

    [InternalsMetadataColumn("status", 6, SqlDbType.Int, 4, 25, 6)]
    public int Status { get; set; }

    [InternalsMetadataColumn("fgidfs", 7, SqlDbType.SmallInt, 2, 29, 7)]
    public short Fgidfs { get; set; }

    [InternalsMetadataColumn("rcrows", 8, SqlDbType.BigInt, 8, 31, 8)]
    public long RowCount { get; set; }

    [InternalsMetadataColumn("cmprlevel", 9, SqlDbType.TinyInt, 1, 39, 9)]
    public byte CompressionLevel { get; set; }

    [InternalsMetadataColumn("fillfact", 10, SqlDbType.TinyInt, 1, 40, 10)]
    public byte FillFactor { get; set; }

    [InternalsMetadataColumn("maxnullbit", 11, SqlDbType.SmallInt, 2, 41, 11)]
    public short Maxnullbit { get; set; }

    [InternalsMetadataColumn("maxleaf", 12, SqlDbType.Int, 4, 43, 12)]
    public int MaxLeaf { get; set; }

    [InternalsMetadataColumn("maxint", 13, SqlDbType.SmallInt, 2, 47, 13)]
    public short MaxInt { get; set; }

    [InternalsMetadataColumn("minleaf", 14, SqlDbType.SmallInt, 2, 49, 14)]
    public short MinLeaf { get; set; }

    [InternalsMetadataColumn("minint", 15, SqlDbType.SmallInt, 2, 51, 15)]
    public short MinInt { get; set; }

    [InternalsMetadataColumn("rsguid", 16, SqlDbType.VarBinary, 16, -1, 16)]
    public byte[] Rsguid { get; set; } = Array.Empty<byte>();

    [InternalsMetadataColumn("lockres", 17, SqlDbType.VarBinary, 8, -2, 17)]
    public byte[] Lockres { get; set; } = Array.Empty<byte>();

    [InternalsMetadataColumn("scope_id", 18, SqlDbType.Int, 4, 53, 18)]
    public int ScopeId { get; set; }

}