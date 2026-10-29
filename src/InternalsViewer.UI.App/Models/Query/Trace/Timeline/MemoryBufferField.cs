using System;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record MemoryBufferField(string Name, string Value, Action? Open = null)
{
    public bool IsLink => Open is not null;

    public bool IsText => Open is null;

    public void OpenTarget() => Open?.Invoke();
}
