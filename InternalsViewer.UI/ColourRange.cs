using System.Drawing;

namespace InternalsViewer.UI
{
    /// <summary>
    /// Colour range for a KeyImageColumn
    /// </summary>
    internal class ColourRange
    {
        internal ColourRange(int from, int to, Color colour)
        {
            this.From = from;
            this.To = to;
            this.Colour = colour;
        }

        /// <summary>
        /// Gets or sets from range value.
        /// </summary>
        /// <value>From.</value>
        public int From { get; set; }

        /// <summary>
        /// Gets or sets to range value.
        /// </summary>
        /// <value>To.</value>
        public int To { get; set; }

        /// <summary>
        /// Gets or sets the colour associated with the given range
        /// </summary>
        /// <value>The colour.</value>
        public Color Colour { get; set; }
    }
}
