using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.UI.App.Models.Query.Trace.CallStack.Values;

public sealed record ValueDescription(string Text, string? ClassName = null)
{
    public MemoryMapEntry? Origin { get; init; }

    public static implicit operator ValueDescription(string text) => new(text);
}
