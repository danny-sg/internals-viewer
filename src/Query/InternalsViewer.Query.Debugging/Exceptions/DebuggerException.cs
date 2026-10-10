namespace InternalsViewer.Query.Debugging.Exceptions;

public sealed class DebuggerException(string message, bool isEngineFailure = false) : Exception(message)
{
    public bool IsEngineFailure { get; } = isEngineFailure;
}
