namespace InternalsViewer.Internals.Engine.Pages.Enums;

[Flags]
public enum StealOptions
{
    NoStealOption = 0,
    ColdBuffer = 1,
    PartOfReservation = 2,
    ForShortTermUse = 4,
    NoZeroing = 8,
    DecommitPage = 16,
    L2DiskIo = 32,
    IsMapped = 64
}
