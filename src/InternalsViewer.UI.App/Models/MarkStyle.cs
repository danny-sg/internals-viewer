using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Models;

/// <summary>
/// Style definition for a mark, including foreground/background colours
/// </summary>
public sealed record MarkStyle
{
    public SolidColorBrush ForeColour { get; set; } = new(Colors.Black);

    public SolidColorBrush BackColour { get; set; } = new(Colors.Transparent);

    public SolidColorBrush AlternateBackColour { get; set; } = new(Colors.Transparent);

    public string Name { get; set; } = string.Empty;

    public int? Ordinal { get; set; }
}