namespace InternalsViewer.Query.FullTrace.Locators.Enums;

public enum LocatorValueType : byte
{
    Page = 0,
    Row = 1,
    ColumnStoreObject = 2,
    Referenced = 3,
    LogSequenceNumber = 5,
    Text = 6,
    WideText = 7,
    TypeInfo = 8,
    Variant = 9,
    Bindings = 10
}
