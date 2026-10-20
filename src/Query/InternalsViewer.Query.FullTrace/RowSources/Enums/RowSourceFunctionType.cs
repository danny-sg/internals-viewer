namespace InternalsViewer.Query.FullTrace.RowSources.Enums;

internal enum RowSourceFunctionType : byte
{
    Iterator,
    BatchHelper,
    BatchIterator,
    BatchFactory,
    Constructor,
    Teardown
}
