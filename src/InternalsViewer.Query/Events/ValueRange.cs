namespace InternalsViewer.Query.Events;

/// <summary>The location of a field value's text within <see cref="EventResult.Buffer"/>. A zero
/// <see cref="Length"/> means an empty or absent value.</summary>
public readonly record struct ValueRange(int Offset, int Length);