namespace InternalsViewer.Mcp.Models.Context.Queries;

public sealed record McpIndicator(Guid Id, string Comment, int Targets, bool HasNext, int Step, int Steps);
