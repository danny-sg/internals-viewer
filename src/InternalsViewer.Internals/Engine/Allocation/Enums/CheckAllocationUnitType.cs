namespace InternalsViewer.Internals.Engine.Allocation.Enums;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
public enum CheckAllocationUnitType
{
    AUT_INVALID = 0,
    AUT_HEAP = 1,
    AUT_CLUST_INDEX = 2,
    AUT_NC_INDEX = 3,
    AUT_LOB = 4,
    AUT_SLOB = 5,
    AUT_DROPPED = 6,
    AUT_SYSTEMPAGE = 7,
    AUT_HOBT = 8
}
