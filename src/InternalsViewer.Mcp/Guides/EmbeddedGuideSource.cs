using System.Reflection;
using InternalsViewer.Mcp.Interfaces.Guides;

namespace InternalsViewer.Mcp.Guides;

public sealed class EmbeddedGuideSource : IMcpGuideSource
{
    private const string Prefix = "InternalsViewer.Mcp.Guides.";

    private const string Extension = ".md";

    private static readonly (string Topic, string Description)[] Known =
    [
        ("reading", "How to read what the tools return: times, positions, pages, folded events, the call tree, locators, arguments"),
        ("events", "What each event type and field means"),
        ("engine-symbols", "How to read SQL Server engine function names and call paths"),
        ("full-trace-investigations", "The method for explaining an operator, a sort, a hash join or a batch from a Full Trace"),
        ("source", "Where the public source code of each part of Internals Viewer lives, for questions nothing else answers")
    ];

    public IEnumerable<McpGuide> GetGuides()
    {
        var assembly = typeof(EmbeddedGuideSource).Assembly;

        var resources = assembly.GetManifestResourceNames()
                                .Where(IsGuide)
                                .ToDictionary(r => r[Prefix.Length..^Extension.Length], r => r);

        foreach (var (topic, description) in Known)
        {
            if (resources.Remove(topic, out var resource))
            {
                yield return new McpGuide(topic, description, () => Read(assembly, resource));
            }
        }

        foreach (var (topic, resource) in resources.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            var description = $"{GetHeading(assembly, resource)}, a deep dive from the documentation";

            yield return new McpGuide(topic, description, () => Read(assembly, resource));
        }
    }

    private static bool IsGuide(string resource)
        => resource.StartsWith(Prefix, StringComparison.Ordinal) && resource.EndsWith(Extension, StringComparison.Ordinal);

    private static string Read(Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)!;

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    private static string GetHeading(Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)!;

        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line[2..].Trim();
            }
        }

        return resource[Prefix.Length..^Extension.Length];
    }
}
