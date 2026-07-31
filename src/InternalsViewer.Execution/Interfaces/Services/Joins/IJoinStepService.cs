using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Services.Joins.Inputs;

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
