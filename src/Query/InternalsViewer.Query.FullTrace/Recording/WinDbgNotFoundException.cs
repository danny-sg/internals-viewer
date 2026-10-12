using InternalsViewer.Query.Recording;

namespace InternalsViewer.Query.FullTrace.Recording;

public sealed class WinDbgNotFoundException(string message) : DebuggerException(message);
