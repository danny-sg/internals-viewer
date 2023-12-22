using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Engine.Pages;

public abstract class AllocationUnitPage
    : Page
{
    /// <summary>
    /// Type of page compression (2008+).
    /// </summary>
    public CompressionType CompressionType { get; set; }

    /// <summary>
    /// CI (Compression Information) structure for compressed data/index pages
    /// </summary>
    public CompressionInfo? CompressionInfo { get; set; }

    public AllocationUnit AllocationUnit { get; set; } = AllocationUnit.Unknown;
}

public class IndexPage
    : AllocationUnitPage;

public class DataPage
    : AllocationUnitPage;