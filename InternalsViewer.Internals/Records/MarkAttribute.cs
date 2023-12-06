using System;

namespace InternalsViewer.Internals.Records;

/// <summary>
/// Custom attribute to store mark properties
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class MarkAttribute(MarkType markType, string description) : Attribute
{
    public string Description { get; set; } = description;

    public MarkType MarkType { get; set; } = markType;

    public MarkAttribute(MarkType markType) : this(markType, null)
    {
    }

    public bool Visible { get; set; }
}