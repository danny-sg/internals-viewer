using System.Data;
using InternalsViewer.Internals.Generators;

namespace InternalsViewer.Internals.Metadata.Internals.Tables;

/// <summary>
/// Columns - sys.syscolpars
/// </summary>
[InternalsMetadata]
public record InternalColumn
{
    [InternalsMetadataColumn("id", 1, SqlDbType.Int, 4, 4, 1)]
    public int ObjectId { get; set; }

    [InternalsMetadataColumn("number", 2, SqlDbType.SmallInt, 2, 8, 2)]
    public short Number { get; set; }

    [InternalsMetadataColumn("colid", 3, SqlDbType.Int, 4, 10, 3)]
    public int ColumnId { get; set; }

    [InternalsMetadataColumn("name", 4, SqlDbType.NVarChar, 256, -1, 4)]
    public string? Name { get; set; } = string.Empty;

    [InternalsMetadataColumn("xtype", 5, SqlDbType.TinyInt, 1, 14, 5)]
    public byte SystemTypeId { get; set; }

    [InternalsMetadataColumn("utype", 6, SqlDbType.Int, 4, 15, 6)]
    public int UserTypeId { get; set; }

    [InternalsMetadataColumn("length", 7, SqlDbType.SmallInt, 2, 19, 7)]
    public short MaxLength { get; set; }

    [InternalsMetadataColumn("prec", 8, SqlDbType.TinyInt, 1, 21, 8)]
    public byte Precision { get; set; }

    [InternalsMetadataColumn("scale", 9, SqlDbType.TinyInt, 1, 22, 9)]
    public byte Scale { get; set; }

    [InternalsMetadataColumn("collationid", 10, SqlDbType.Int, 4, 23, 10)]
    public int CollationId { get; set; }

    [InternalsMetadataColumn("status", 11, SqlDbType.Int, 4, 27, 11)]
    public int Status { get; set; }

    [InternalsMetadataColumn("maxinrow", 12, SqlDbType.SmallInt, 2, 31, 12)]
    public short MaxInRow { get; set; }

    [InternalsMetadataColumn("xmlns", 13, SqlDbType.Int, 4, 33, 13)]
    public int XmlNamespaceId { get; set; }

    [InternalsMetadataColumn("dflt", 14, SqlDbType.Int, 4, 37, 14)]
    public int Dflt { get; set; }

    [InternalsMetadataColumn("chk", 15, SqlDbType.Int, 4, 41, 15)]
    public int Chk { get; set; }

    [InternalsMetadataColumn("idtval", 16, SqlDbType.VarBinary, 64, -2, 16)]
    public byte[]? IdtVal { get; set; } = Array.Empty<byte>();
}