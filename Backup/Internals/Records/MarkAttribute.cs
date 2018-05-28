using System;
using System.Drawing;

namespace InternalsViewer.Internals.Records
{
    /// <summary>
    /// Custom attribute to store mark properties
    /// </summary>
    [AttributeUsage(System.AttributeTargets.Property)]
    public class MarkAttribute : System.Attribute
    {
        private string description;
        private Color foreColour = Color.Black;
        private Color backColour = Color.White;
        private Color alternateBackColour;
        private bool visible;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkAttribute"/> class.
        /// </summary>
        /// <param name="description">The description.</param>
        public MarkAttribute(string description)
        {
            this.Description = description;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkAttribute"/> class.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <param name="foreColour">The fore colour.</param>
        /// <param name="backColour">The back colour.</param>
        /// <param name="visible">if set to <c>true</c> [visible].</param>
        public MarkAttribute(string description, string foreColour, string backColour, bool visible)
        {
            this.Description = description;
            this.ForeColour = Color.FromName(foreColour);
            this.BackColour = Color.FromName(backColour);
            this.AlternateBackColour = this.BackColour;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkAttribute"/> class.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <param name="foreColour">The fore colour.</param>
        /// <param name="backColour">The back colour.</param>
        /// <param name="alternateBackColour">The alternate back colour.</param>
        /// <param name="visible">if set to <c>true</c> [visible].</param>
        public MarkAttribute(string description, string foreColour, string backColour, string alternateBackColour, bool visible)
        {
            this.Description = description;
            this.ForeColour = Color.FromName(foreColour);
            this.BackColour = Color.FromName(backColour);
            this.AlternateBackColour = Color.FromName(alternateBackColour);
        }

        /// <summary>
        /// Gets or sets the mark display description.
        /// </summary>
        /// <value>The description.</value>
        public string Description
        {
            get { return description; }
            set { description = value; }
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
        /// Gets or sets the back colour.
        /// </summary>
        /// <value>The back colour.</value>
        public Color BackColour
        {
            get { return backColour; }
            set { backColour = value; }
        }

        /// <summary>
        /// Gets or sets the alternate back colour.
        /// </summary>
        /// <value>The alternate back colour.</value>
        public Color AlternateBackColour
        {
            get { return alternateBackColour; }
            set { alternateBackColour = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="MarkAttribute"/> is visible.
        /// </summary>
        /// <value><c>true</c> if visible; otherwise, <c>false</c>.</value>
        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }
    }
}
