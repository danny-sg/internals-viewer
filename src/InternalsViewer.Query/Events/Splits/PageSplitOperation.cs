namespace InternalsViewer.Query.Events.Splits;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
public enum PageSplitOperation
{
    SPLIT_FOR_INSERT = 0,
    SPLIT_FOR_GHOST = 1,
    SPLIT_FOR_DELETE = 2,
    SPLIT_FOR_UPDATE = 3,
    SPLIT_FOR_INTERNAL_NODE = 4,
    SPLIT_FOR_ROOT_NODE = 5,
    SPLIT_EMPTY_BTREE = 6,
    SPLIT_FOR_REVERT = 7,
    SPLIT_FOR_NEW_PAGE = 8,
    COUNT_SPLIT_OP = 9
}
