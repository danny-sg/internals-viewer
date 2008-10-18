using System.Drawing;

namespace InternalsViewer.Internals.Markers
{
    /// <summary>
    /// Defines a marker properties
    /// </summary>
    public class MarkerDefinition
    {
        private string name;        
        private MarkerType markerType;
        private Color backColour;
        private Color foreColour;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkerDefinition"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="markerType">Type of the marker.</param>
        /// <param name="backColour">The back colour.</param>
        /// <param name="foreColour">The fore colour.</param>
        public MarkerDefinition(string name, MarkerType markerType, Color backColour, Color foreColour)
        {
            this.Name = name;
            this.MarkerType = markerType;
            this.BackColour = backColour;
            this.ForeColour = foreColour;
        }

        /// <summary>
        /// Gets or sets the back colour.
        /// </summary>
        /// <value>The back colour.</value>
        public Color BackColour
        {
            get { return backColour; }
            set { backColour = value; }
        }

        /// <summary>
        /// Gets or sets the fore colour.
        /// </summary>
        /// <value>The fore colour.</value>
        public Color ForeColour
        {
            get { return foreColour; }
            set { foreColour = value; }
        }

        /// <summary>
        /// Gets or sets the type of the marker.
        /// </summary>
        /// <value>The type of the marker.</value>
        public MarkerType MarkerType
        {
            get { return markerType; }
            set { markerType = value; }
        }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return name; }
            set { name = value; }
        }

    }
}
