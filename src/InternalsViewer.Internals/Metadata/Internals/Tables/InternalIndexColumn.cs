using System.Data;
using InternalsViewer.Internals.Generators;

namespace InternalsViewer.Internals.Metadata.Internals.Tables;

/// <summary>
/// sys.sysiscols
/// </summary>
[InternalsMetadata]
public record InternalIndexColumn
{
    [InternalsMetadataColumn("idmajor", 1, SqlDbType.Int, 4, 1, 1)]
    public int ObjectId { get; set; }

    [InternalsMetadataColumn("idminor", 2, SqlDbType.Int, 4, 5, 2)]
    public int IndexId { get; set; }

    [InternalsMetadataColumn("subid", 3, SqlDbType.Int, 4, 9, 3)]
    public int IndexColumnId { get; set; }

    [InternalsMetadataColumn("status", 4, SqlDbType.Int, 4, 13, 4)]
    public int Status { get; set; }

    [InternalsMetadataColumn("intprop", 5, SqlDbType.Int, 4, 17, 5)]
    public int ColumnId { get; set; }

    [InternalsMetadataColumn("tinyprop1", 6, SqlDbType.TinyInt, 1, 21, 6)]
    public byte KeyOrdinal { get; set; }

    [InternalsMetadataColumn("tinyprop2", 7, SqlDbType.TinyInt, 1, 22, 7)]
    public byte PartitionOrdinal { get; set; }

    [InternalsMetadataColumn("tinyprop3", 8, SqlDbType.TinyInt, 1, 23, 8)]
    public byte TinyProp3 { get; set; }

    [InternalsMetadataColumn("tinyprop4", 9, SqlDbType.TinyInt, 1, 24, 9)]
    public byte ColumnStoreOrderOrdinal { get; set; }
}