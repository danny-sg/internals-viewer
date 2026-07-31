using InternalsViewer.Execution.AccessPaths.Joins;

namespace InternalsViewer.Execution.Interfaces.Services.Joins;

/// <summary>
/// A step service that reads two inputs and combines their rows
/// </summary>
public interface IJoinStepService : IStepService
{
    IJoinInput Outer { get; }

    IJoinInput Inner { get; }

    JoinType JoinType { get; }
}
