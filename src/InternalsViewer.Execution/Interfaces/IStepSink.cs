using InternalsViewer.Execution.AccessPaths.Results;

namespace InternalsViewer.Execution.Interfaces;

public interface IStepSink
{
    ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken);
}
