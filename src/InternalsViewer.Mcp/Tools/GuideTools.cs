using System.ComponentModel;
using InternalsViewer.Mcp.Interfaces.Guides;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools;

[McpServerToolType]
public sealed class GuideTools(IEnumerable<IMcpGuideSource> sources)
{
    private IEnumerable<IMcpGuideSource> Sources { get; } = sources;

    [McpServerTool(Name = "guide", ReadOnly = true, Idempotent = true)]
    [Description("A reference for reading the data and the app. Omit the topic to list them. Start with reading. app describes the " +
                 "views, bands and options as the user sees them. The deep-dive topics explain how the tracing works. source maps " +
                 "the public code for questions nothing else answers")]
    public string Guide([Description("The topic to read")] string? topic = null)
    {
        var guides = Sources.SelectMany(s => s.GetGuides()).ToList();

        var name = topic?.Trim().ToLowerInvariant();

        var guide = name is null ? null : guides.FirstOrDefault(g => g.Topic == name);

        if (guide is null)
        {
            var listed = string.Join(Environment.NewLine, guides.Select(g => $"- {g.Topic}: {g.Description}"));

            return name is null ? $"Guides:{Environment.NewLine}{listed}" : $"No guide {topic}. Guides:{Environment.NewLine}{listed}";
        }

        return guide.Read();
    }
}
