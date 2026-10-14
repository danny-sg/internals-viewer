namespace InternalsViewer.Query.QueryStore;

// ReSharper disable InconsistentNaming
public enum QueryStoreState
{
    OFF = 0,
    READ_ONLY = 1,
    READ_WRITE = 2,
    ERROR = 3,
    READ_CAPTURE_SECONDARY = 4
}
