using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Mcp.Models.Context;

public abstract record AllocationUnitContext : TabContext
{
    public AllocationUnit? AllocationUnit { get; init; }
}
