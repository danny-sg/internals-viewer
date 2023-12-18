using System.Drawing;

namespace InternalsViewer.UI.MarkStyles;

public class MarkStyle
{
    public MarkStyle(Color foreColour, Color backColour, Color alternateBackColour)
    {
        ForeColour = foreColour;
        BackColour = backColour;
        AlternateBackColour = alternateBackColour;
    }

    public MarkStyle(Color foreColour, Color backColour)
    {
        ForeColour = foreColour;
        BackColour = backColour;
    }

    public MarkStyle(Color foreColour, Color backColour, string description)
    {
        ForeColour = foreColour;
        BackColour = backColour;
        Description = description;
    }
        
    public MarkStyle(Color foreColour, Color backColour, Color alternateBackColour, string description)
    {
        ForeColour = foreColour;
        BackColour = backColour;
        AlternateBackColour = alternateBackColour;
        Description = description;
    }

    /// <summary>
    /// Gets or sets the mark display description.
    /// </summary>
    /// <value>The description.</value>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the fore colour.
    /// </summary>
    /// <value>The fore colour.</value>
    public Color ForeColour { get; set; } = Color.Black;

    /// <summary>
    /// Gets or sets the back colour.
    /// </summary>
    /// <value>The back colour.</value>
    public Color BackColour { get; set; } = Color.White;

    /// <summary>
    /// Gets or sets the alternate back colour.
    /// </summary>
    /// <value>The alternate back colour.</value>
    public Color AlternateBackColour { get; set; } = Color.White;
}