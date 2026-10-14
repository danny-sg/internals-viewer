namespace InternalsViewer.Query.CallStack.Locators;

public enum LocatorKind : byte
{
    Page,
    Row,
    ColumnStoreObject,
    Referenced,
    Argument,
    LogSequenceNumber,
    Text,
    WideText,
    TypeInfo
}
