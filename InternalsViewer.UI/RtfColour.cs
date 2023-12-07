using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using InternalsViewer.UI.Properties;

namespace InternalsViewer.UI;

/// <summary>
/// Create RTF colour elements 
/// </summary>
internal class RtfColour
{
    private RtfColour()
    {
        var colours = new[]
        {
            Color.Blue,
        }
    }

    /// <summary>
    /// Creates the RTF header.
    /// </summary>
    /// <param name="rtfColours">The RTF colours.</param>
    /// <returns></returns>
    public static string CreateRtfHeader(List<Color> rtfColours)
    {
        var sb = new StringBuilder(Resources.Rtf_HeaderStart);

        if (rtfColours != null)
        {
            foreach (var c in rtfColours)
            {
                sb.AppendFormat(Resources.Rtf_ColourTable, c.R, c.G, c.B);
            }
        }

        sb.Append(Resources.Rtf_HeaderEnd);

        return sb.ToString();
    }

    /// <summary>
    /// Create an rtf tag with a fore colour and back colour
    /// </summary>
    /// <param name="rtfColours">The RTF colours.</param>
    /// <param name="foreColour">The fore colour.</param>
    /// <param name="backColour">The back colour.</param>
    /// <returns></returns>
    public static string RtfTag(List<Color> rtfColours, string foreColour, string backColour)
    {
        var foreColourIndex = rtfColours.FindIndex(col => col.Name == foreColour) + 1;
        var backColourIndex = rtfColours.FindIndex(col => col.Name == backColour) + 1;

        return string.Format(CultureInfo.InvariantCulture,
                             Resources.Rtf_ColourHighlightTag,
                             foreColourIndex,
                             backColourIndex);
    }

    /// <summary>
    /// Creates the RTF colour table for the given list of colours
    /// </summary>
    /// <returns></returns>
    public static List<Color> CreateColourTable(List<string> colours)
    {
        var rtfColours = new List<Color>();

        foreach (var name in colours)
        {
            rtfColours.Add(Color.FromName(name));
        }

        return rtfColours;
    }
}

public enum TagColour
{
    White,
    Black,
}