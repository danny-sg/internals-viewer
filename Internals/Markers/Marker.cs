using System.Collections.Generic;

namespace InternalsViewer.Internals.Markers
{
    /// <summary>
    /// Defines a region of data
    /// </summary>
    public class Marker
    {
        private MarkerDefinition definition;
        private int startPosition;
        private int endPosition;
        private bool isNull;
        private string value;
        private bool visible;

        /// <summary>
        /// Initializes a new instance of the <see cref="Marker"/> class.
        /// </summary>
        /// <param name="definition">The definition.</param>
        /// <param name="startPosition">The start position.</param>
        /// <param name="endPosition">The end position.</param>
        public Marker(MarkerDefinition definition, int startPosition, int endPosition)
        {
            this.Definition = definition;
            this.StartPosition = startPosition;
            this.EndPosition = endPosition;

            visible = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Marker"/> class.
        /// </summary>
        /// <param name="definition">The definition.</param>
        /// <param name="startPosition">The start position.</param>
        /// <param name="endPosition">The end position.</param>
        /// <param name="value">The value.</param>
        public Marker(MarkerDefinition definition, int startPosition, int endPosition, string value)
        {
            this.Definition = definition;
            this.StartPosition = startPosition;
            this.EndPosition = endPosition;
            this.Value = value;

            visible = true;
        }

        /// <summary>
        /// Get a list of 0-n markers with a position between a given range
        /// </summary>
        /// <param name="startPosition">The start position.</param>
        /// <param name="endPosition">The end position.</param>
        /// <param name="markers">The markers.</param>
        /// <returns></returns>
        public static Marker GetMarkerAtPosition(int startPosition, int endPosition, List<Marker> markers)
        {
            return markers.Find(delegate(Marker marker)
                                {
                                    return marker.StartPosition >= startPosition & marker.endPosition <= endPosition;
                                });
        }

        #region Properties

        /// <summary>
        /// Gets or sets the marker definition.
        /// </summary>
        /// <value>The definition.</value>
        public MarkerDefinition Definition
        {
            get { return definition; }
            set { definition = value; }
        }

        /// <summary>
        /// Gets or sets the start position.
        /// </summary>
        /// <value>The start position.</value>
        public int StartPosition
        {
            get { return startPosition; }
            set { startPosition = value; }
        }

        /// <summary>
        /// Gets or sets the end position.
        /// </summary>
        /// <value>The end position.</value>
        public int EndPosition
        {
            get { return endPosition; }
            set { endPosition = value; }
        }

        /// <summary>
        /// Gets or sets the marker display value.
        /// </summary>
        /// <value>The value.</value>
        public string Value
        {
            get { return value; }
            set { this.value = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the value is null.
        /// </summary>
        /// <value><c>true</c> if this instance is null; otherwise, <c>false</c>.</value>
        public bool IsNull
        {
            get { return isNull; }
            set { isNull = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Marker"/> is visible.
        /// </summary>
        /// <value><c>true</c> if visible; otherwise, <c>false</c>.</value>
        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }

        #endregion
    }
}
