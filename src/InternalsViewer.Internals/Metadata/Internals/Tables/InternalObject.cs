using System.Data;
using InternalsViewer.Internals.Generators;
// ReSharper disable StringLiteralTypo

namespace InternalsViewer.Internals.Metadata.Internals.Tables;

/// <summary>
/// SQL Server Allocation Units definitions table - sys.sysschobjs    
/// </summary>
[InternalsMetadata]
public record InternalObject
{
    [InternalsMetadataColumn("id", 1, SqlDbType.Int, 4, 4, 1)]
    public int ObjectId { get; set; }

    [InternalsMetadataColumn("name", 2, SqlDbType.NVarChar, 4, -1, 2)]
    public string Name { get; set; } = string.Empty;

    [InternalsMetadataColumn("nsid", 3, SqlDbType.Int, 4, 8, 3)]
    public int SchemaId { get; set; }

    [InternalsMetadataColumn("nsclass", 4, SqlDbType.TinyInt, 1, 12, 4)]
    public byte NamespaceClass { get; set; }

    [InternalsMetadataColumn("status", 5, SqlDbType.Int, 4, 13, 5)]
    public int Status { get; set; }

    [InternalsMetadataColumn("type", 6, SqlDbType.Char, 2, 17, 6)]
    public string ObjectType { get; set; } = string.Empty;

    [InternalsMetadataColumn("pid", 7, SqlDbType.Int, 4, 19, 7)]
    public int ParentObjectId { get; set; }

    [InternalsMetadataColumn("pclass", 7, SqlDbType.TinyInt, 1, 23, 8)]
    public byte ParentClass { get; set; }

    [InternalsMetadataColumn("intprop", 7, SqlDbType.Int, 4, 24, 9)]
    public int IntProperty { get; set; }

    [InternalsMetadataColumn("created", 10, SqlDbType.DateTime, 8, 28, 10)]
    public DateTime CreatedDateTime { get; set; }

    [InternalsMetadataColumn("modified", 11, SqlDbType.DateTime, 8, 36, 11)]
    public DateTime ModifiedDateTime { get; set; }

    [InternalsMetadataColumn("status2", 12, SqlDbType.Int, 4, 44, 12)]
    public int Status2 { get; set; }
}