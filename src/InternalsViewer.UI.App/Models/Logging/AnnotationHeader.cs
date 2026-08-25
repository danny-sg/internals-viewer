namespace InternalsViewer.UI.App.Models.Logging;

/// <summary>
/// Marker for the column header row shown above a log record's annotation rows
/// </summary>
public sealed class AnnotationHeader
{
    public static readonly AnnotationHeader Instance = new();

    private AnnotationHeader()
    {
    }
}
