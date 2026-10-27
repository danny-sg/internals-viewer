using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Mcp.Models.Context.Queries;

/// <summary>
/// A function's signature resolved from its symbols, with the layout of its arguments
/// </summary>
public sealed record McpFunctionSignature(string Text, ArgumentLayout Layout);
