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
        /// <summary>
        /// Initializes a new instance of the <see cref="MarkAttribute"/> class.
        /// </summary>
        /// <param name="description">The description.</param>
        public MarkAttribute(string description)
        {
            Description = description;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkAttribute"/> class.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <param name="foreColour">The fore colour.</param>
        /// <param name="backColour">The back colour.</param>
        public MarkAttribute(string description, string foreColour, string backColour)
        {
            Description = description;
            ForeColour = Color.FromName(foreColour);
            BackColour = Color.FromName(backColour);
            AlternateBackColour = BackColour;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkAttribute"/> class.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <param name="foreColour">The fore colour.</param>
        /// <param name="backColour">The back colour.</param>
        /// <param name="alternateBackColour">The alternate back colour.</param>
        public MarkAttribute(string description, string foreColour, string backColour, string alternateBackColour)
        {
            Description = description;
            ForeColour = Color.FromName(foreColour);
            BackColour = Color.FromName(backColour);
            AlternateBackColour = Color.FromName(alternateBackColour);
        }

        /// <summary>
        /// Gets or sets the mark display description.
        /// </summary>
        /// <value>The description.</value>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the fore colour.
        /// </summary>
        /// <value>The fore colour.</value>
        public Color ForeColour { get; set; } = Color.Black;

        /// <summary>
        /// Gets or sets the back colour.
        /// </summary>
        /// <value>The back colour.</value>
        public Color BackColour { get; set; } = Color.White;

        /// <summary>
        /// Gets or sets the alternate back colour.
        /// </summary>
        /// <value>The alternate back colour.</value>
        public Color AlternateBackColour { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="MarkAttribute"/> is visible.
        /// </summary>
        /// <value><c>true</c> if visible; otherwise, <c>false</c>.</value>
        public bool Visible { get; set; }
    }
}
