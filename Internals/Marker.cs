using System.Collections.Generic;
using System.Drawing;

namespace InternalsViewer.Internals
{
    public class Marker
    {
        private Color backColour;
        private int endPosition;
        private Color foreColour;
        private bool isNull;
        private MarkerType markerType;
        private string name;
        private int startPosition;
        private string value;
        private bool visible;

        public Marker(string name, MarkerType markerType, int start, int end, Color backColour, Color foreColour)
        {
            this.Name = name;
            this.StartPosition = start;
            this.EndPosition = end;
            this.BackColour = backColour;
            this.ForeColour = foreColour;
            this.MarkerType = markerType;

            visible = true;
        }

        public static Marker GetMarkerAtPosition(int startPosition, int endPosition, List<Marker> markers)
        {
            return markers.Find(delegate(Marker marker) 
                               { 
                                   return marker.StartPosition >= startPosition & marker.endPosition <= endPosition; 
                               });
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

        #region Properties

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public int StartPosition
        {
            get { return startPosition; }
            set { startPosition = value; }
        }

        public int EndPosition
        {
            get { return endPosition; }
            set { endPosition = value; }
        }

        public Color BackColour
        {
            get { return backColour; }
            set { backColour = value; }
        }

        public Color ForeColour
        {
            get { return foreColour; }
            set { foreColour = value; }
        }

        public string Value
        {
            get { return value; }
            set { this.value = value; }
        }

        public MarkerType MarkerType
        {
            get { return markerType; }
            set { markerType = value; }
        }

        public bool IsNull
        {
            get { return isNull; }
            set { isNull = value; }
        }

        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }

        #endregion
    }
}
