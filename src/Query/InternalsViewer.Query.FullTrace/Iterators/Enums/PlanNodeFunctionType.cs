namespace InternalsViewer.Query.FullTrace.Iterators.Enums;

internal enum PlanNodeFunctionType : byte
{
    Iterator,
    BatchHelper,
    BatchIterator,
    BatchFactory,
    Constructor,
    Teardown
}
