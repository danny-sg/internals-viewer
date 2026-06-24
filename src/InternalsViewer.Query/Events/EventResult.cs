namespace InternalsViewer.Query.Events;

public sealed record EventResult
{
    public int SequenceId { get; set; }

    public required string Name { get; set; }

    public DateTime Timestamp { get; set; }

    public int DatabaseId { get; set; }

    public Dictionary<string, object?> Data { get; set; } = new();

    public Dictionary<string, object?> Actions { get; set; } = new();
}