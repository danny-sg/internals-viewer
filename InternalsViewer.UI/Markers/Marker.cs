using System.Drawing;
using System.Collections.Generic;

namespace InternalsViewer.UI.Markers
{
    public class Marker
    {
        private int endPosition;
        private int startPosition;
        private Color foreColour;
        private Color backColour;
        private Color alternateBackColour;
        private bool isNull;
        private MarkerType markerType;
        private string name;
        private string value;
        private bool visible;

        public Marker()
        {
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

        public Color AlternateBackColour
        {
            get { return alternateBackColour; }
            set { alternateBackColour = value; }
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