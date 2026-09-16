using InternalsViewer.Execution.Common.AccessPaths.Results.Steps;
using InternalsViewer.Execution.Common.Interfaces;

namespace InternalsViewer.Execution.Common.AccessPaths.Results;

public sealed class NullStepSink : IStepSink
{
    private NullStepSink()
    {
    }

    public static NullStepSink Instance { get; } = new();

    public ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
