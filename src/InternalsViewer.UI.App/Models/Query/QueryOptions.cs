namespace InternalsViewer.UI.App.Models.Query;

public record QueryOptions
{
    public bool ClearBufferPool { get; set; } = false;

    public bool DisableReadAhead { get; set; } = true;
}
