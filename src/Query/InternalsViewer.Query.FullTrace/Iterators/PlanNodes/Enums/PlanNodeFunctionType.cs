namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;

internal enum PlanNodeFunctionType : byte
{
    Iterator,
    BatchHelper,
    BatchIterator,
    BatchFactory,
    Constructor,
    Teardown
}
