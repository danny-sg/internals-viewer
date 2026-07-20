namespace InternalsViewer.UI.App.Models;

/// <summary>
/// Marker for the column header row shown above a log record's annotation rows
/// </summary>
/// <remarks>
/// A single shared instance is used as the content of a header node prepended to each record's annotation
/// children, so the nested Offset / Length / change columns are labelled where they appear
/// </remarks>
public sealed class AnnotationHeader
{
    public static readonly AnnotationHeader Instance = new();

    private AnnotationHeader()
    {
    }
}
