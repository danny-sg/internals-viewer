namespace InternalsViewer.Query.Debugging;

/// <summary>
/// The named pipe a WinDbg session listens on for Internals Viewer and the password guarding it
/// </summary>
/// <remarks>
/// WinDbg needs elevated permissions to attach to the SQL Server process. It accepts an unelevated client via a named pipe.
/// </remarks>
public sealed record WinDbgServerOptions(string Pipe, string? Password)
{
    /// <summary>
    /// Client connect options
    /// </summary>
    public string RemoteOptions => $"npipe:Pipe={Pipe},Server=localhost{PasswordOption("Password")}";

    /// <summary>
    /// WinDbg command to start listening
    /// </summary>
    public string ServerCommand => $".server {Transport}";

    /// <summary>
    /// Command line switch that has WinDbg listen from startup
    /// </summary>
    public string LaunchSwitch => $"-server {Transport}";

    private string Transport => $"npipe:pipe={Pipe}{PasswordOption("password")}";

    private string PasswordOption(string name) => string.IsNullOrWhiteSpace(Password) ? string.Empty : $",{name}={Password}";
}
