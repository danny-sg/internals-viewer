using InternalsViewer.Execution.Common.AccessPaths.Results.Steps;

namespace InternalsViewer.Execution.Common.Interfaces;

public interface IStepSink
{
    ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken);
}
