using System.Data;

namespace InternalsViewer.Internals.Metadata.Internals;

/// <summary>
///     Parses the ti (Type Info) field of the sys.sysallocunits table
/// </summary>
public record TypeInfo
{
    public byte Scale { get; set; }

    public byte Precision { get; set; }

    public short MaxLength { get; set; }

    public short MaxInRowLength { get; set; }

    public SqlDbType DataType { get; set; }
}