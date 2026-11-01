using Windows.UI;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public readonly record struct MemoryReferenceGraphSpan(double Start, double End, Color? Colour);
