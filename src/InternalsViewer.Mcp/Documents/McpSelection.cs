using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Mcp.Documents;

public sealed record McpSelection
{
    public EngineEvent? Event { get; init; }

    public PlanNode? PlanNode { get; init; }

    public CallStackNode? CallNode { get; init; }

    public TracePosition? Playhead { get; init; }

    public long? PlayheadTimeUs { get; init; }

    public McpTraceRange? TraceRange { get; init; }

    public McpTimeRange? EventRange { get; init; }

    public McpTimeRange? Scope { get; init; }

    public McpPageSelection[] Pages { get; init; } = [];

    public string[] VisibleViews { get; init; } = [];

    public bool IsEmpty => this is { Event: null, PlanNode: null, CallNode: null, Playhead: null, PlayheadTimeUs: null, Pages.Length: 0 };
}
