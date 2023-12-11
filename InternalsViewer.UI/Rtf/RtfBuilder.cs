using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;
using InternalsViewer.UI.Markers;
using InternalsViewer.UI.MarkStyles;

namespace InternalsViewer.UI.Rtf;

internal class RtfBuilder
{
    private static readonly string RtfLineBreak = @"\par " + Environment.NewLine;

    private static readonly Color HeaderColour = Color.FromArgb(245, 245, 250);
    private static readonly Color OffsetColour = Color.FromArgb(245, 250, 245);

    /// <summary>
    ///  RTF Header
    ///      \rtf1        - RTF Version 1
    ///      \ansi        - ANSI character set
    ///      \ansicpg1252 - ANSI code page 1252
    ///      \deff0       - Default font is first font in font table
    ///      \deflang1033 - Default language is English (US)
    /// </summary>
    private const string HeaderStart = @"{\rtf1\ansi\ansicpg1252\deff0\deflang1033";

    /// <summary>
    /// RTF Font Table
    ///     \fonttbl     - Font table
    ///     \f0          - Font 0
    ///     \fnil        - Unknown font family
    ///     \fcharset0   - ANSI character set
    ///     \Courier New - Font name
    /// </summary>
    private const string FontTable = @"{\fonttbl{\f0\fnil\fcharset0 Courier New;}}";

    /// <summary>
    /// Colour Table Header
    /// </summary>
    private const string ColourTable = @"{\colortbl ;";

    /// <summary>
    /// Colour Table Entry as R/G/B value
    /// 
    /// Colour is selected from the 1 based colour table entry index
    /// </summary>
    private const string ColourTableEntry = @"\red{0}\green{1}\blue{2};";

    /// <summary>
    /// Content Start
    ///     \viewkind4 - Normal View
    ///     \uc1       - Unicode char 1
    ///     \pard      - Paragraph Default
    ///     \f0        - Font 0
    ///     \fs17      - Font Size 17
    /// </summary>
    private const string ContentStart = @"\viewkind4\uc1\pard\f0\fs17";

    /// <summary>
    /// Colour Highlight Tag start
    /// 
    /// \highlight - Highlight colour (1-based index)
    /// \cf        - Foreground colour (1-based index)
    /// </summary>
    private const string ColourHighlightTag = @"{{\cf{0}\highlight{1} ";

    /// <summary>
    /// Creates the start of the RTF document
    /// 
    /// This includes the RTF header, Font Table, Colour Table and Content Start
    /// </summary>
    /// <returns></returns>
    private static string CreateHeader(List<Color> rtfColours)
    {
        var sb = new StringBuilder(HeaderStart);

        sb.Append(FontTable);
        sb.AppendLine(ColourTable);

        if (rtfColours != null)
        {
            foreach (var c in rtfColours)
            {
                sb.AppendFormat(ColourTableEntry, c.R, c.G, c.B);
            }
        }

        sb.AppendLine(@"}");

        sb.Append(ContentStart);

        var defaultColours = GetColourIndexes(rtfColours, Color.Black.Name, Color.White.Name);

        sb.AppendFormat(@"\cf{0}\highlight{1}", defaultColours.ForeColourIndex, defaultColours.BackColourIndex);

        return sb.ToString();
    }

    /// <summary>
    /// Create an rtf tag with a fore colour and back colour
    /// </summary>
    public static string RtfTag(List<Color> rtfColours, string foreColour, string backColour)
    {
        var (foreColourIndex, backColourIndex) = GetColourIndexes(rtfColours, foreColour, backColour);

        return string.Format(CultureInfo.InvariantCulture,
                             ColourHighlightTag,
                             foreColourIndex,
                             backColourIndex);
    }

    private static (int ForeColourIndex, int BackColourIndex) GetColourIndexes(List<Color> rtfColours, string foreColour, string backColour)
    {
        var foreColourIndex = rtfColours.FindIndex(col => col.Name == foreColour);

        if (foreColourIndex == -1)
        {
            throw new KeyNotFoundException($"Colour {foreColour} not found in colour table");
        }

        var backColourIndex = rtfColours.FindIndex(col => col.Name == backColour);

        if (backColourIndex == -1)
        {
            throw new KeyNotFoundException($"Colour {backColour} not found in colour table");
        }

        return (foreColourIndex + 1, backColourIndex + 1);
    }

    public static string BuildRtf(Page targetPage, List<Marker> markers, bool colourise, int selectedOffset)
    {
        var standardColours = new[]
        {
            Color.Black, Color.White, Color.Blue,Color.Green, Color.PaleGoldenrod , HeaderColour, OffsetColour
        };

        var markerStyles = new MarkStyleProvider().Styles.Values;

        var rtfColours = standardColours.Union(markerStyles.Select(s => s.BackColour)).Union(markerStyles.Select(s => s.ForeColour)).Distinct().ToList();

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

        for (var rows = 0; rows < targetPage.PageData.Length / 16; rows++)
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
                    sb.Append(@"{\uldb ");
                }

                if (currentPos == Page.Size - targetPage.Header.SlotCount * 2)
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
                sb.Append(DataConverter.ToHexString(targetPage.PageData[currentPos]));

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

    /// <summary>
    /// Find if a marker starts at a given positions, and if it does add a start RTF colour tag
    /// </summary>
    private static bool FindStartMarkers(int position, StringBuilder sb, bool alternate, List<Marker> markers, List<Color> rtfColours)
    {
        var startMarkers = markers.Where(marker => marker.StartPosition == position).ToList();

        foreach (var startMarker in startMarkers)
        {
            sb.Append(RtfTag(rtfColours,
                             startMarker.ForeColour.Name,
                             alternate ? startMarker.AlternateBackColour.Name : startMarker.BackColour.Name));

            alternate = !alternate;
        }

        return alternate;
    }

    /// <summary>
    /// Find if a marker ends at a given positions, and if it does add a close RTF colour tag
    /// </summary>
    private static void FindEndMarkers(int position, StringBuilder sb, List<Marker> markers)
    {
        if (position <= 0)
        {
            return;
        }

        var endMarkers = markers.Where(marker => marker.EndPosition == position).ToList();

        for (var i = 0; i < endMarkers.Count; i++)
        {
            sb.Append(@"}");
        }
    }
}
