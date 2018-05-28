using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using InternalsViewer.UI.Properties;

namespace InternalsViewer.UI
{
    /// <summary>
    /// Create RTF colour elements 
    /// </summary>
    internal class RtfColour
    {
        private RtfColour()
        {
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
        /// Create an rtf tag with a forecolour and backcolour
        /// </summary>
        /// <param name="rtfColours">The RTF colours.</param>
        /// <param name="foreColour">The fore colour.</param>
        /// <param name="backColour">The back colour.</param>
        /// <returns></returns>
        public static string RtfTag(List<Color> rtfColours, string foreColour, string backColour)
        {
            int foreColourIndex;
            int backColourIndex;

            foreColourIndex = rtfColours.FindIndex(delegate(Color col) { return col.Name == foreColour; }) + 1;
            backColourIndex = rtfColours.FindIndex(delegate(Color col) { return col.Name == backColour; }) + 1;

            return string.Format(CultureInfo.InvariantCulture,
                                 Resources.Rtf_ColourHighlightTag,
                                 foreColourIndex,
                                 backColourIndex);
        }

        /// <summary>
        /// Creates the RTF colour table for the given list of colours
        /// </summary>
        /// <returns></returns>
        public static List<Color> CreateColourTable()
        {
            var rtfColours = new List<Color>();

            foreach (var name in Enum.GetNames(typeof(KnownColor)))
            {
                rtfColours.Add(Color.FromName(name));
            }

            return rtfColours;
        }
    }
}
