using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Models;

public class MarkStyle
{
    /// <summary>
    /// Gets or sets the fore colour.
    /// </summary>
    /// <value>The fore colour.</value>
    public SolidColorBrush ForeColour { get; set; } = new(Colors.Black);

    /// <summary>
    /// Gets or sets the back colour.
    /// </summary>
    /// <value>The back colour.</value>
    public SolidColorBrush BackColour { get; set; } = new(Colors.Transparent);

    /// <summary>
    /// Gets or sets the alternate back colour.
    /// </summary>
    /// <value>The alternate back colour.</value>
    public SolidColorBrush AlternateBackColour { get; set; } = new(Colors.Transparent);

    public string Name { get; set; } = string.Empty;
}