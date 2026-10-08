namespace InternalsViewer.Query.Events;

/// <summary>
/// Buffer based event result for a single event
/// </summary>
/// <remarks>
/// Event parsing is span/buffer based, meaning that this represents the mappings of an event on the buffer rather than the parsed data
/// itself.
///
/// It allows properties to be evaluated and extracted without having to materialize the whole object.
/// 
/// Buffer is a char[], and arrays are reference types, so this holds a reference to the char[] buffer and ValueRange - positions in the
/// buffer for the different Data and Action properties.
///
/// Name and the Data/Action keys are interned, so this whole record is just a set of references/value type properties with no heap
/// allocation once initialized. It is designed to be reused for loading multiple events so should be treated as transient and not stored
/// or cached.
/// </remarks>
public sealed record EventResult
{
    public int SequenceId { get; set; }

    public required string Name { get; set; }

    public DateTime Timestamp { get; set; }

    public int DatabaseId { get; set; }

    public char[] Buffer { get; set; } = [];

    public Dictionary<string, ValueRange> Data { get; set; } = new();

    public Dictionary<string, ValueRange> Actions { get; set; } = new();
}