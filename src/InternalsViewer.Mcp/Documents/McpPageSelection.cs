using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Mcp.Documents;

public sealed record McpPageSelection(PageAddress Address, bool IsActive, int? Slot, string? Field, int? FieldOffset, string? LogRecord);
