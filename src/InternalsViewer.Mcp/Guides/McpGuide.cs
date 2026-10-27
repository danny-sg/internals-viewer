namespace InternalsViewer.Mcp.Guides;

public sealed record McpGuide(string Topic, string Description, Func<string> Read);
