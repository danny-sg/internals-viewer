using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Metadata.Internals;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Engine.Database;

public sealed class DatabaseSource(IConnectionType connection) : DatabaseSummary
{
    public IConnectionType Connection { get; set; } = connection;

    public BootPage BootPage { get; set; } = null!;

    /// <summary>
    /// GAM (Global Allocation Map) chain per database file
    /// </summary>
    public Dictionary<int, AllocationChain> Gam { get; set; } = [];

    /// <summary>
    /// SGAM (Shared Global Allocation Map) chain per database file
    /// </summary>
    public Dictionary<int, AllocationChain> SGam { get; set; } = [];

    /// <summary>
    /// DCM (Differential Changed Map) chain per database file
    /// </summary>
    public Dictionary<int, AllocationChain> Dcm { get; set; } = [];

    /// <summary>
    /// BCM (Bulk Changed Map) chain per database file
    /// </summary>
    public Dictionary<int, AllocationChain> Bcm { get; set; } = [];

    /// <summary>
    /// PFS (Page Free Space) chain per database file
    /// </summary>
    public Dictionary<int, PfsChain> Pfs { get; set; } = [];

    public bool IsCompatible => CompatibilityLevel >= 90 && State == 0;

    public List<DatabaseFile> Files { get; set; } = [];

    public Dictionary<long, AllocationUnit> AllocationUnits { get; set; } = [];

    /// <summary>
    /// Table structures cached by AllocationUnitId.
    /// </summary>
    public Dictionary<long, TableStructure> TableStructures { get; } = [];

    /// <summary>
    /// Index structures cached by AllocationUnitId.
    /// </summary>
    public Dictionary<long, IndexStructure> IndexStructures { get; } = [];

    public InternalMetadata Metadata { get; set; } = new();

    public int GetFileSize(short fileId)
    {
        return Files.FirstOrDefault(file => file.FileId == fileId)?.Size ?? 0;
    }
}