using System.Drawing;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Plans;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace InternalsViewer.Query.Events.EventTypes;

public record EngineEvent
{
    public int DatabaseId { get; set; }

    public int SequenceId { get; set; }

    public DateTime Timestamp { get; set; }

    public string Name { get; set; } = string.Empty;

    public long TimeUs { get; set; }

    public long DurationUs { get; set; }

    public PageAddress? PageAddress { get; set; }

    public int ObjectId { get; set; }

    public string ObjectName { get; set; } = string.Empty;

    public string SchemaName { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public string IndexName { get; set; } = string.Empty;

    internal string PlanHandle { get; set; } = string.Empty;

    public int ThreadId { get; set; }

    public virtual string Description => string.Empty;

    public PlanNodeIdentifier? PlanNodeIdentifier { get; set; }

    public Color DisplayColour { get; set; }
}
