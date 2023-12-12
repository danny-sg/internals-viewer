using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace InternalsViewer.UI.Rtf;

internal class DateTimeRtfBuilder : RtfBuilder
{
    public static string BuildRtf(string date, string time, Color backColour)
    {
        var rtfColours = new List<Color>
        {
            Color.Blue, Color.Green, backColour
        };

        var rtfHeader = CreateHeader(rtfColours);

        var sb = new StringBuilder(rtfHeader);

        sb.Append(RtfTag(rtfColours, Color.Blue.Name, backColour.Name));
        sb.Append(time);
        sb.Append("} ");
        sb.Append(RtfTag(rtfColours, Color.Green.Name, backColour.Name));
        sb.Append(date);
        sb.Append("}");

        return sb.ToString();
    }   
}
