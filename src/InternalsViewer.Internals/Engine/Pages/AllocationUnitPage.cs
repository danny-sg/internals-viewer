using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Engine.Pages;

public abstract class AllocationUnitPage
    : Page
{
    /// <summary>
    /// If page compression is active
    /// </summary>
    /// <remarks>
    /// Page Compression can be turned on against the object, but not all pages will necessarily be compressed, for example if the page
    /// is not full it will use row compression only.
    /// 
    /// The type flag bit 0x80 (128 / bit 8) determines if the page is compressed.
    /// </remarks>
    public bool IsPageCompressed => (PageHeader.TypeFlagBits & 0x80) != 0;

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