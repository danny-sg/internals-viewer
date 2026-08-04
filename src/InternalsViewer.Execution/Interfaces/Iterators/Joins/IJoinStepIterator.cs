using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.Interfaces.Iterators.Joins.Inputs;

namespace InternalsViewer.Execution.Interfaces.Iterators.Joins;

/// <summary>
/// A step service that reads two inputs and combines their rows
/// </summary>
public interface IJoinStepIterator : IStepIterator
{
    IJoinInput Outer { get; }

    IJoinInput Inner { get; }

    JoinType JoinType { get; }
}
