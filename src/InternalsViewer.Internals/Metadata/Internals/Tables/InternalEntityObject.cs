using System.Data;
using InternalsViewer.Internals.Generators;

// ReSharper disable StringLiteralTypo

namespace InternalsViewer.Internals.Metadata.Internals.Tables;

/// <summary>
/// Entities by class - sys.sysclsobjs
/// </summary>
/// <remarks>
///  Contains a row for each classified entity that shares the same common properties that include the following:
///
///                            Class Id
///     - Assembly              10
///     - Audit Specification   65
///     - Availability Group    67
///     - Backup device         55
///     - Credentials           57
///     - Cryptographic provider 63
///     - Full-text catalog     32
///     - Full-text stop list   33
///     - Partition function    30
///     - Partition scheme      31 + Type = PS
///     - File group            31 + Type IN (FD, FG, FL)
///     - Obfuscation key
///     - Key          56
///     - Schema                50
///     - External Resource Pool  125
/// </remarks>
[InternalsMetadata]
public record InternalEntityObject
{
    [InternalsMetadataColumn("class", 1, SqlDbType.TinyInt, 1, 4, 1)]
    public byte ClassId { get; set; }

    [InternalsMetadataColumn("id", 2, SqlDbType.Int, 4, 5, 2)]
    public int Id { get; set; }

    [InternalsMetadataColumn("name", 3, SqlDbType.NVarChar, 256, -1, 3)]
    public string Name { get; set; } = string.Empty;

    [InternalsMetadataColumn("id", 4, SqlDbType.Int, 4, 9, 4)]
    public int Status { get; set; }

    [InternalsMetadataColumn("type", 5, SqlDbType.Char, 2, 13, 5)]
    public string Type { get; set; } = string.Empty;

    [InternalsMetadataColumn("id", 6, SqlDbType.Int, 4, 15, 6)]
    public int IntProp { get; set; }

    [InternalsMetadataColumn("created", 7, SqlDbType.DateTime, 8, 19, 7)]
    public DateTime CreatedDateTime { get; set; }

    [InternalsMetadataColumn("modified", 8, SqlDbType.DateTime, 8, 27, 8)]
    public DateTime ModifiedDateTme { get; set; }
}