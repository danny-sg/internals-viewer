namespace InternalsViewer.Internals.Engine.Pages.Enums;

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