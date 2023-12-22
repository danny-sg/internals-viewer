using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Engine.Pages;

/// <summary>
/// Page Data
/// </summary>
public class PageData : DataStructure
{
    /// <summary>
    /// Page size is 8192 bytes/8KB
    /// </summary>
    public const int Size = 8192;

    /// <summary>
    /// Database the page belongs to
    /// </summary>
    public DatabaseDetail Database { get; init; } = null!;

    /// <summary>
    /// Page Address in the format File Id : Page Id
    /// </summary>
    public PageAddress PageAddress { get; init; }

    /// <summary>
    /// Raw page data
    /// </summary>
    public byte[] Data { get; set; } = new byte[Size];

    /// <summary>
    /// Page Header
    /// </summary>
    public PageHeader PageHeader { get; init; } = new();

    /// <summary>
    /// Table/Array containing the data offset of each row in the page
    /// </summary>
    public List<ushort> OffsetTable { get; init; } = new();
}