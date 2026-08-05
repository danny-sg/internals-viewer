using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.Execution.Interfaces;

public interface IStepSink
{
    ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken);
}
