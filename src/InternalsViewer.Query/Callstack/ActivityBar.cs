namespace InternalsViewer.Query.Callstack;

/// <summary>
/// One bar of a node's activity histogram: its height and colour (the selected event's bucket is highlighted)
/// </summary>
public sealed record ActivityBar(double Height, string Colour);
