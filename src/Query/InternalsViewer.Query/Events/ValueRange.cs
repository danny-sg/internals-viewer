namespace InternalsViewer.Query.Events;

/// <summary>
/// The location of a field value's text within <see cref="EventResult.Buffer"/>
/// </summary>
/// <remarks>A zero <see cref="Length"/> means an empty or absent value.
/// </remarks>
public readonly record struct ValueRange(int Offset, int Length);