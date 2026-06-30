namespace InternalsViewer.Query.Events;

public sealed record EventResult
{
    public int SequenceId { get; set; }

    public required string Name { get; set; }

    public DateTime Timestamp { get; set; }

    public int DatabaseId { get; set; }

    // The character buffer the field values point into (reused across events). Values are stored as ranges
    // into this buffer and only materialised into a string when actually read as one, so the many values
    // that are never read - or are read as numbers - don't each allocate a string.
    public char[] Buffer { get; set; } = [];

    public Dictionary<string, ValueRange> Data { get; set; } = new();

    public Dictionary<string, ValueRange> Actions { get; set; } = new();
}

/// <summary>The location of a field value's text within <see cref="EventResult.Buffer"/>. A zero
/// <see cref="Length"/> means an empty or absent value.</summary>
public readonly record struct ValueRange(int Offset, int Length);
