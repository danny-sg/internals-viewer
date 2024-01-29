namespace InternalsViewer.Internals.Engine.Pages.Enums;

/// <summary>
/// Page Type
/// </summary>
public enum PageType
{
    None = 0,
    FileHeader = 15,
    Data = 1,
    Index = 2,
    Lob3 = 3,
    Lob4 = 4,
    Sort = 7,
    Gam = 8,
    Sgam = 9,
    Iam = 10,
    Pfs = 11,
    Boot = 13,
    Dcm = 16,
    Bcm = 17
}

public enum AllocationUnitPageFlags
{
    AllocNonLogged = 0x20,
    HasChecksum = 0x200,
    VersionInfo = 0x2000,
    AddBeg = 0x4000,
    AddEnd = 0x8000,
    HasFreeSlot = 0x10000,
    PgAligned4 = 0x2,
    FixedLengthRow = 0x4
}