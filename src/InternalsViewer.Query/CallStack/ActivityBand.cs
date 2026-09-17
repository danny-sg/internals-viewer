namespace InternalsViewer.Query.CallStack;

/// <summary>
/// Call frame activity as a heat band
/// </summary>
public sealed record ActivityBand(IReadOnlySet<int> Markers);
