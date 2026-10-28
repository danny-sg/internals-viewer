namespace InternalsViewer.Mcp.Hosting;

public sealed record McpHostSettings(int Port, string Token)
{
    public const int DefaultPort = 8484;

    public const string Path = "/mcp";

    public const string ServerName = "internals-viewer";

    public string Endpoint => $"http://127.0.0.1:{Port}{Path}";

    public string AuthorizationHeader => $"Bearer {Token}";
}
