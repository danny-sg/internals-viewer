using System;
using System.Drawing;

namespace InternalsViewer.UI
{
    public class OffsetEventArgs : EventArgs
    {
        private UInt16 offset;
        private string markerDescription;
        private Color foreColour;
        private Color backColour;


        public OffsetEventArgs(UInt16 offset, string markerDescription, Color foreColour, Color backColour)
        {
            Offset = offset;
            MarkerDescription = markerDescription;
            ForeColour = foreColour;
            BackColour = backColour;
        }

        public Color ForeColour
        {
            get { return foreColour; }
            set { foreColour = value; }
        }

        public Color BackColour
        {
            get { return backColour; }
            set { backColour = value; }
        }

        public string MarkerDescription
        {
            get { return markerDescription; }
            set { markerDescription = value; }
        }

        public UInt16 Offset
        {
            get { return offset; }
            set { offset = value; }
        }
    }
}
