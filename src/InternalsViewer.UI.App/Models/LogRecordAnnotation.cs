namespace InternalsViewer.UI.App.Models;

/// <summary>
/// XAML bindable view of a log record apply change span
/// </summary>
/// <remarks>
/// Mirrors ChangeSpan with mutable properties - the XAML type info generator cannot handle the init-only members
/// on the Query record types, so bound types stay plain
/// </remarks>
public class LogRecordAnnotation
{
    public int Offset { get; set; }

    public int Length { get; set; }

    public string Description { get; set; } = string.Empty;
}
