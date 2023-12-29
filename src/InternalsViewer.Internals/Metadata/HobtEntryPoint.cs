using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Metadata;

/// <summary>
/// The entry point pages for a HOBT
/// 
/// See https://learn.microsoft.com/en-us/sql/relational-databases/pages-and-extents-architecture-guide
/// </summary>
public struct HobtEntryPoint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HobtEntryPoint"/> struct.
    /// </summary>
    public HobtEntryPoint(PageAddress firstIam, PageAddress rootPage, PageAddress firstPage, int partitionNumber)
    {
        FirstIam = firstIam;
        RootPage = rootPage;
        FirstPage = firstPage;
        PartitionNumber = partitionNumber;
    }

    /// <summary>
    /// First IAM page address.
    /// </summary>
    /// <remarks>
    /// The first page in the HOBTs allocation IAM chain
    /// </remarks>
    public PageAddress FirstIam { get; set; }

    /// <summary>
    /// Index root page address.
    /// </summary>
    /// <remarks>
    /// The root page of the b-tree (index)
    /// </remarks>
    public PageAddress RootPage { get; set; }

    /// <summary>
    /// First page address
    /// </summary>
    /// <remarks>
    /// The first page at the leaf/heap level
    /// </remarks>
    public PageAddress FirstPage { get; set; }

    /// <summary>
    /// Partition number.
    /// </summary>
    public int PartitionNumber { get; set; }
}