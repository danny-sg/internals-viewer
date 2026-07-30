using System.Threading;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Interfaces.Services;

public interface IStepService
{
    IReadOnlyList<AccessStep> History { get; }

    AccessStep? Current { get; }

    bool IsComplete { get; }

    PageAddress? CurrentPageAddress { get; }

    AccessStrategy? Strategy { get; }

    Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken);
}
