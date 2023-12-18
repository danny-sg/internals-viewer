using System.Collections.Generic;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Engine.Pages;

/// <summary>
/// Database Page
/// </summary>
public class Page : DataStructure
{
    /// <summary>
    /// Page size is 8192 bytes/8KB
    /// </summary>
    public const int Size = 8192;

    /// <summary>
    /// Type of page compression (2008+).
    /// </summary>
    public CompressionType CompressionType { get; set; }

    /// <summary>
    /// Database the page belongs to
    /// </summary>
    public DatabaseDetail Database { get; set; } = new();

    /// <summary>
    /// Page Address in the format File Id : Page Id
    /// </summary>
    public PageAddress PageAddress { get; set; }

    /// <summary>
    /// Raw page data
    /// </summary>
    public byte[] PageData { get; set; } = new byte[Size];

    /// <summary>
    /// Page Header
    /// </summary>
    public PageHeader PageHeader { get; set; } = new();

    /// <summary>
    /// Table/Array containing the data offset of each row in the page
    /// </summary>
    public List<ushort> OffsetTable { get; } = new();

    /// <summary>
    /// CI (Compression Information) structure for compressed data/index pages
    /// </summary>
    public CompressionInfo? CompressionInfo { get; set; }

    /// <summary>
    /// Allocation Unit the page belongs to
    /// </summary>
    public AllocationUnit AllocationUnit { get; set; } = AllocationUnit.Unknown;
}