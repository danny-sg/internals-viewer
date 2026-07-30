using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.Interfaces;

public interface IStepService
{
    IReadOnlyList<AccessStep> History { get; }

    AccessStep? Current { get; }

    bool IsComplete { get; }

    PageAddress? CurrentPageAddress { get; }

    AccessStrategy? Strategy { get; }

    Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken);
}
