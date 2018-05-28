using System;
using System.Drawing;

namespace InternalsViewer.UI
{
    public class OffsetEventArgs : EventArgs
    {
        public OffsetEventArgs(ushort offset, string markerDescription, Color foreColour, Color backColour)
        {
            Offset = offset;
            MarkerDescription = markerDescription;
            ForeColour = foreColour;
            BackColour = backColour;
        }

        public Color ForeColour { get; set; }

        public Color BackColour { get; set; }

        public string MarkerDescription { get; set; }

        public ushort Offset { get; set; }
    }
}
