using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace InternalsViewer.UI.Markers
{
    public class MarkerDefinition
    {
        private Color foreColour;
        private Color backColour;
        private MarkerType markerType;
        private string name;
        private bool visible;

        public MarkerDefinition(Color foreColour, Color backColour, MarkerType markerType, string name, bool visible)
        {
            this.ForeColour = foreColour;
            this.BackColour = backColour;
            this.MarkerType = markerType;
            this.Name = name;
            this.Visible = visible;
        }

        public Color ForeColour
        {
            get { return this.foreColour; }
            set { this.foreColour = value; }
        }

        public Color BackColour
        {
            get { return this.backColour; }
            set { this.backColour = value; }
        }

        public MarkerType MarkerType
        {
            get { return this.markerType; }
            set { this.markerType = value; }
        }

        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }
    }
}
