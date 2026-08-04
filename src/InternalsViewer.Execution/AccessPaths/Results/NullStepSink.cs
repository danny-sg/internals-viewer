using InternalsViewer.Execution.Interfaces;

namespace InternalsViewer.Execution.AccessPaths.Results;

public sealed class NullStepSink : IStepSink
{
    private NullStepSink()
    {
    }

    public static NullStepSink Instance { get; } = new();

    public ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
