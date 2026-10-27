using System.Collections.Concurrent;
using System.Reflection;

namespace InternalsViewer.Mcp.Prompts;

/// <summary>
/// Reads the prompt texts embedded from the Prompts folder
/// </summary>
public static class PromptStore
{
    private const string Prefix = "InternalsViewer.Mcp.Prompts.";

    private const string Extension = ".md";

    private static readonly ConcurrentDictionary<string, string> Cache = [];

    /// <summary>
    /// Gets the text of a prompt by its file name without the extension
    /// </summary>
    public static string Get(string name) => Cache.GetOrAdd(name, Read);

    private static string Read(string name)
    {
        var assembly = typeof(PromptStore).Assembly;

        using var stream = assembly.GetManifestResourceStream($"{Prefix}{name}{Extension}")
                           ?? throw new InvalidOperationException($"No prompt text named {name}.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd().TrimEnd();
    }
}
