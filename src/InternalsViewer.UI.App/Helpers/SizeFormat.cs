namespace InternalsViewer.UI.App.Helpers;

/// <summary>
/// Formats a byte count for display
/// </summary>
public static class SizeFormat
{
    public static string Format(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024d / 1024d:N1} MB",
        >= 1024 => $"{bytes / 1024d:N1} KB",
        _ => $"{bytes} B"
    };
}
