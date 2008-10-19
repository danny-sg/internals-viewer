using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace InternalsViewer.UI.Markers
{
    public class Marker
    {
        private int endPosition;
        private int startPosition;
        private Color foreColour;
        private Color backColour;
        private bool isNull;
        private MarkerType markerType;
        private string name;

        private string value;
        private bool visible;

        public Marker(MarkerDefinition definition, int startPosition, int endPosition)
        {
            this.D
        }

        public Marker(string name, MarkerType markerType, int startPosition, int endPosition, Color backColour, Color foreColour)
        {
            this.name = name;
            this.startPosition = startPosition;
            this.endPosition = endPosition;
            this.backColour = backColour;
            this.foreColour = foreColour;
            this.markerType = markerType;
            visible = true;
        }

        public static Marker GetMarkerAtPosition(int startPosition, int endPosition, List<Marker> markers)
        {
            return markers.Find(delegate(Marker marker) { return marker.StartPosition >= startPosition & marker.endPosition <= endPosition; });
        }

        public override string ToString()
        {
            return string.Format("{0}: Start:{1} End:{2} Back Colour:{3} Fore Colour:{4}",
                                 name,
                                 startPosition,
                                 endPosition,
                                 backColour.Name,
                                 ForeColour.Name);
        }

        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        public int StartPosition
        {
            get { return this.startPosition; }
            set { this.startPosition = value; }
        }

        public int EndPosition
        {
            get { return this.endPosition; }
            set { this.endPosition = value; }
        }

        public Color BackColour
        {
            get { return this.backColour; }
            set { this.backColour = value; }
        }

        public Color ForeColour
        {
            get { return this.foreColour; }
            set { this.foreColour = value; }
        }

        public string Value
        {
            get { return this.value; }
            set { this.value = value; }
        }

        public MarkerType DataType
        {
            get { return this.markerType; }
            set { this.markerType = value; }
        }

        public bool IsNull
        {
            get { return this.isNull; }
            set { this.isNull = value; }
        }

        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }
    }
}