using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.Models.Page;

public record PageBookmark
{
    public string DatabaseName { get; set; } = string.Empty;

    public PageAddress PageAddress { get; set; }
}
