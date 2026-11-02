namespace InternalsViewer.Mcp.Models.Context.Queries;

public sealed record McpWatch(string Object,
                              int? Block,
                              int Offset,
                              int Length,
                              string? DataType,
                              string? Label,
                              string? Value,
                              bool IsTemporary);
