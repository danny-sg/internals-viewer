using InternalsViewer.Execution.AccessPaths.Results.Joins;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.Interfaces;

/// <summary>
/// A step service composed of an outer and inner access path, each with its own strategy
/// </summary>
public interface IJoinStepService : IStepService
{
    AccessStrategy? OuterStrategy { get; }

    AccessStrategy? InnerStrategy { get; }

    IReadOnlyList<JoinBufferRow> OuterBuffer { get; }

    IReadOnlyList<JoinBufferRow> InnerBuffer { get; }
}
