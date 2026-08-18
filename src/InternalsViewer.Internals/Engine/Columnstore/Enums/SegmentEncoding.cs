namespace InternalsViewer.Internals.Engine.Columnstore.Enums;

public enum SegmentEncoding
{
    Unknown = 0,
    ValueBased = 1,
    ValueHashBased = 2,
    StringHashBased = 3,
    StoreByValueBased = 4,
    StringStoreByValueBased = 5
}