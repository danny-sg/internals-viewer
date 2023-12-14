using System.Drawing;

namespace InternalsViewer.UI;

/// <summary>
/// Colour range for a KeyImageColumn
/// </summary>
internal class ColourRange
{
    internal ColourRange(int from, int to, Color colour)
    {
        From = from;
        To = to;
        Colour = colour;
    }

    public int From { get; set; }

    public int To { get; set; }

    public Color Colour { get; set; }
}