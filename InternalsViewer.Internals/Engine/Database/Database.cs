using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Engine.Database;

public class Database : DatabaseInfo
{
    public BootPage BootPage { get; set; } = null!;

    /// <summary>
    /// GAM (Global Allocation Map) chain per database file
    /// </summary>
    public Dictionary<int, AllocationChain> Gam { get; set; } = new();

    /// <summary>
    /// SGAM (Shared Global Allocation Map) chain per database file
    /// </summary>
    public Dictionary<int, AllocationChain> SGam { get; set; } = new();


    /// <summary>
    /// DCM (Differential Changed Map) chain per database file
    /// </summary>
    public Dictionary<int, AllocationChain> Dcm { get; set; } = new();


    /// <summary>
    /// BCM (Bulk Changed Map) chain per database file
    /// </summary>
    public Dictionary<int, AllocationChain> Bcm { get; set; } = new();

    /// <summary>
    /// PFS (Page Free Space) chain per database file
    /// </summary>
    public Dictionary<int, PfsChain> Pfs { get; set; } = new();

    public List<DatabaseFile> Files { get; set; } = new();

    public bool IsCompatible => CompatibilityLevel >= 90 && State == 0;

    public int GetFileSize(short fileId)
    {
        return Files.FirstOrDefault(file => file.FileId == fileId)?.Size ?? 0;
    }

    public List<AllocationUnit> AllocationUnits { get; set; } = new();
}