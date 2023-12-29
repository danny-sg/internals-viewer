using System.Data;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Generators;

// ReSharper disable StringLiteralTypo

namespace InternalsViewer.Internals.Metadata.Internals.Tables;

/// <summary>
/// sys.sysidxstats
/// </summary>
[InternalsMetadata]
public record InternalIndex
{
    [InternalsMetadataColumn("id", 1, SqlDbType.Int, 4, 4, 1)]
    public int ObjectId { get; set; }

    [InternalsMetadataColumn("indid", 2, SqlDbType.Int, 4, 8, 2)]
    public int IndexId { get; set; }

    [InternalsMetadataColumn("name", 3, SqlDbType.NVarChar, 256, -1, 3)]
    public string Name { get; set; } = string.Empty;

    [InternalsMetadataColumn("status", 4, SqlDbType.Int, 4, 12, 4)]
    public int Status { get; set; }

    [InternalsMetadataColumn("intprop", 5, SqlDbType.Int, 4, 16, 5)]
    public int IntProp { get; set; }

    [InternalsMetadataColumn("fillfact", 6, SqlDbType.TinyInt, 1, 20, 6)]
    public byte FillFactor { get; set; }

    [InternalsMetadataColumn("type", 7, SqlDbType.TinyInt, 1, 21, 7)]
    public IndexType IndexType { get; set; }

    [InternalsMetadataColumn("tinyprop", 8, SqlDbType.TinyInt, 1, 22, 8)]
    public byte TinyProp { get; set; }

    [InternalsMetadataColumn("dataspace", 9, SqlDbType.Int, 4, 23, 9)]
    public int DataSpace { get; set; }

    [InternalsMetadataColumn("lobds", 10, SqlDbType.Int, 4, 27, 10)]
    public int Lobds { get; set; }

    [InternalsMetadataColumn("rowset", 11, SqlDbType.BigInt, 8, 31, 11)]
    public long RowSet { get; set; }
}