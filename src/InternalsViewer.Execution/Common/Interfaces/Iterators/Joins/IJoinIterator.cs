using InternalsViewer.Execution.Common.AccessPaths.Joins;

namespace InternalsViewer.Execution.Common.Interfaces.Iterators.Joins;

public interface IJoinIterator : IIterator
{
    IJoinInput Outer { get; }

    IJoinInput Inner { get; }

    JoinType JoinType { get; }

    int PairCount { get; }
}
