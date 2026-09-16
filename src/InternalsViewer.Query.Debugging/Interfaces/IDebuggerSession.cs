namespace InternalsViewer.Query.Debugging.Interfaces;

/// <summary>
/// A connected debugger session where commands are sent
/// </summary>
public interface IDebuggerSession : IDisposable
{
    Task SendAsync(string command, CancellationToken cancellationToken);
}
