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
        public string Description { get; set; }

        public MarkType MarkType { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkAttribute"/> class.
        /// </summary>
        public MarkAttribute(MarkType markType)
        {
            MarkType = markType;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MarkAttribute"/> class.
        /// </summary>
        public MarkAttribute(MarkType markType, string description)
        {
            MarkType = markType;
            Description = description;
        }



        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="MarkAttribute"/> is visible.
        /// </summary>
        /// <value><c>true</c> if visible; otherwise, <c>false</c>.</value>
        public bool Visible { get; set; }
    }
}
