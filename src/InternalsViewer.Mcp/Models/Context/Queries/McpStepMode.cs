namespace InternalsViewer.Mcp.Models.Context.Queries;

/// <summary>
/// What a step of the Trace Timeline playhead moves to
/// </summary>
public enum McpStepMode
{
    Call,
    SameCall,
    Batch,
    Row,
    Allocation
}
