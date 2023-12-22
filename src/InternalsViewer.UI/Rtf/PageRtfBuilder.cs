using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.UI.Markers;
using InternalsViewer.UI.MarkStyles;

namespace InternalsViewer.UI.Rtf;

internal class PageRtfBuilder: RtfBuilder
{
    public static string BuildRtf(Page? targetPage, List<Marker> markers, bool colourise, int selectedOffset)
    {
        var standardColours = new[]
        {
            Color.Black, Color.White, Color.Blue,Color.Green, Color.PaleGoldenrod , HeaderColour, OffsetColour
        };

        var markerStyles = new MarkStyleProvider().Styles.Values;

        var rtfColours = standardColours.Union(markerStyles.Select(s => s.BackColour))
                                        .Union(markerStyles.Select(s => s.ForeColour))
                                        .Distinct()
                                        .ToList();

        var rtfHeader = CreateHeader(rtfColours);

        if (targetPage == null)
        {
            return string.Empty;
        }

        var alternate = false;

        var sb = new StringBuilder();

        sb.AppendLine(rtfHeader);

        // Start header
        sb.AppendLine(RtfTag(rtfColours, Color.Blue.Name, HeaderColour.Name));

        var currentPos = 0;

        for (var rows = 0; rows < targetPage.Data.Length / 16; rows++)
        {
            for (var cols = 0; cols < 16; cols++)
            {
                if (currentPos == 96)
                {
                    // End Header
                    sb.Append(@"}");
                }

                if (currentPos == selectedOffset)
                {
                    // Double underline
                    sb.Append(@"{\uldb ");
                }

                if (currentPos == Page.Size - targetPage.PageHeader.SlotCount * 2)
                {
                    // Start offset table
                    sb.Append(RtfTag(rtfColours, Color.Green.Name, OffsetColour.Name));
                }

                // Start marker/colour tag
                if (colourise)
                {
                    alternate = FindStartMarkers(currentPos, sb, alternate, markers, rtfColours);
                }

                // Add the byte
                sb.Append(DataConverter.ToHexString(targetPage.Data[currentPos]));

                // End marker/close colour tag
                if (colourise)
                {
                    FindEndMarkers(currentPos, sb, markers);
                }

                if (currentPos == selectedOffset)
                {
                    sb.Append(@"}");
                }

                if (cols != 15)
                {
                    sb.Append(" ");
                }

                currentPos++;
            }

            sb.Append(RtfLineBreak);
        }

        // Close the document and the RTF file
        sb.Append(@"}}");

        return sb.ToString();
    }

}
