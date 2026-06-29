namespace InternalsViewer.UI.App.Models.Query;

public record QueryOptions
{
    public bool ClearBufferPool { get; set; } = true;

    public bool DisableReadAhead { get; set; } = true;
}
