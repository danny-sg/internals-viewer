using InternalsViewer.Mcp.Guides;

namespace InternalsViewer.Mcp.Interfaces.Guides;

public interface IMcpGuideSource
{
    IEnumerable<McpGuide> GetGuides();
}
