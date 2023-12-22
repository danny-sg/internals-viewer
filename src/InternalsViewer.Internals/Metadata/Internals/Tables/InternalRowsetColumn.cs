using System.Data;
using InternalsViewer.Internals.Generators;

// ReSharper disable IdentifierTypo

namespace InternalsViewer.Internals.Metadata.Internals.Tables;

/// <summary>
/// SQL Server Column Layout definitions table - sys.sysrscols
/// </summary>
/// <remarks>
/// Table only accessible via DAC connection (prefix connection with admin:)
/// 
/// This table is needed to understand the structure of the row
/// 
/// Relevant fields are:
/// 
///     Leaf Offset   - offset & 0xffff
///     Is Uniquifier - status & 16
///     Is Dropped    - status & 2
///     Is Sparse     - status & 0x00000100
///     /// </remarks>
[InternalsMetadata]
public record InternalColumnLayout
{
    [InternalsMetadataColumn("rsid", 1, SqlDbType.BigInt, 8, 4, 1)]
    public long PartitionId { get; set; }

    [InternalsMetadataColumn("rscolid", 2, SqlDbType.Int, 4, 12, 2)]
    public int ColumnId { get; set; }

    [InternalsMetadataColumn("bcolid", 3, SqlDbType.Int, 4, 16, 3)]
    public int Bcolid { get; set; }

    [InternalsMetadataColumn("rcmodified", 4, SqlDbType.BigInt, 8, 20, 4)]
    public long Rcmodified { get; set; }

    /// <remarks>
    /// <see href="https://improve.dk/creating-a-type-aware-parser-for-the-sys-system_internals_partition_columns-ti-field/"/>
    /// </remarks>
    [InternalsMetadataColumn("ti", 5, SqlDbType.Int, 4, 28, 5)]
    public int TypeInfo { get; set; }

    [InternalsMetadataColumn("cid", 6, SqlDbType.Int, 4, 32, 6)]
    public int CollationId { get; set; }

    [InternalsMetadataColumn("ordkey", 7, SqlDbType.SmallInt, 2, 36, 7)]
    public short KeyOrdinal { get; set; }

    [InternalsMetadataColumn("maxinrowlen", 8, SqlDbType.SmallInt, 2, 38, 8)]
    public short Maxrowinlen { get; set; }

    [InternalsMetadataColumn("status", 9, SqlDbType.Int, 4, 40, 9)]
    public int Status { get; set; }

    [InternalsMetadataColumn("offset", 10, SqlDbType.Int, 4, 44, 10)]
    public int Offset { get; set; }

    [InternalsMetadataColumn("nullbit", 11, SqlDbType.Int, 4, 48, 11)]
    public int NullBit { get; set; }

    [InternalsMetadataColumn("bitpos", 12, SqlDbType.SmallInt, 4, 52, 12)]
    public short BitPosition { get; set; }

    [InternalsMetadataColumn("colguid", 13, SqlDbType.VarBinary, 16, -1, 13)]
    public byte[] PartitionColumnGuid { get; set; } = Array.Empty<byte>();

    [InternalsMetadataColumn("ordlock", 14, SqlDbType.Int, 4, 54, 14)]
    public int Ordlock { get; set; }
}