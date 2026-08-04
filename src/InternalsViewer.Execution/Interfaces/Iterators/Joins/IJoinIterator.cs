using InternalsViewer.Execution.AccessPaths.Joins;

namespace InternalsViewer.Execution.Interfaces.Iterators.Joins;

public interface IJoinIterator : IIterator
{
    IJoinInput Outer { get; }

    IJoinInput Inner { get; }

    JoinType JoinType { get; }

    int PairCount { get; }
}
