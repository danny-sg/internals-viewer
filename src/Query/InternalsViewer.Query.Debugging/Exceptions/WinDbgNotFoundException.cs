namespace InternalsViewer.Query.Debugging.Exceptions;

public sealed class WinDbgNotFoundException(string message) : DebuggerException(message);
