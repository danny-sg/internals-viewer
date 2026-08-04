using InternalsViewer.Execution.Interfaces;

namespace InternalsViewer.Execution.AccessPaths.Results;

public sealed class NullStepSink : IStepSink
{
    public static NullStepSink Instance { get; } = new();

    private NullStepSink()
    {
    }

    public ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
