namespace InternalsViewer.Query.FullTrace.RowSources.Enums;

internal enum RowSourceFunctionType : byte
{
    Iterator,
    BatchIterator,
    BatchFactory,
    Constructor,
    Teardown
}
