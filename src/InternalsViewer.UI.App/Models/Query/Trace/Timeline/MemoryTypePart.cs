using System;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record MemoryTypePart(string Text, Action? Open)
{
    public bool IsLink => Open is not null;

    public bool IsText => Open is null;
}
