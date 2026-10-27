namespace InternalsViewer.Mcp.Clients;

public sealed class McpClientEnvironment(string homeDirectory, string applicationDataDirectory, Func<string, string?> findExecutable)
{
    public static McpClientEnvironment Default { get; } =
        new(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            FindOnPath);

    public string HomeDirectory { get; } = homeDirectory;

    public string ApplicationDataDirectory { get; } = applicationDataDirectory;

    private Func<string, string?> FindExecutableCallback { get; } = findExecutable;

    public string? FindExecutable(string name) => FindExecutableCallback(name);

    public string GetHomePath(params string[] parts) => Path.Combine([HomeDirectory, .. parts]);

    public string GetApplicationDataPath(params string[] parts) => Path.Combine([ApplicationDataDirectory, .. parts]);

    private static string? FindOnPath(string name)
    {
        string[] extensions = OperatingSystem.IsWindows() ? [".exe", ".cmd", ".bat", string.Empty] : [string.Empty];

        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in directories)
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, name + extension);

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
