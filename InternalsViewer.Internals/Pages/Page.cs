using System.Collections.Generic;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Pages;

/// <summary>
/// Database Page
/// </summary>
public class Page : DataStructure
{
    public const int Size = 8192;

    /// <summary>
    /// Gets or sets the type of page compression (2008+).
    /// </summary>
    public CompressionType CompressionType { get; set; }

    /// <summary>
    /// Gets the database.
    /// </summary>
    public Database Database { get; set; } = new();

    /// <summary>
    /// Gets or sets the page address.
    /// </summary>
    public PageAddress PageAddress { get; set; }

    /// <summary>
    /// Gets or sets the page data.
    /// </summary>
    public byte[] PageData { get; set; } =  new byte[Size];

    /// <summary>
    /// Gets or sets the header.
    /// </summary>
    public Header Header { get; set; } = new();

    /// <summary>
    /// Gets the offset table.
    /// </summary>
    public List<ushort> OffsetTable { get; } = new();

    public CompressionInfo? CompressionInfo { get; set; }
}