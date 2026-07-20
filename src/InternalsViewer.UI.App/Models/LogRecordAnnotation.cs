using InternalsViewer.Internals.Annotations;
using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.App.Models;

/// <summary>
/// XAML bindable view of a log record apply change span
/// </summary>
/// <remarks>
/// Mirrors ChangeSpan with mutable properties - the XAML type info generator cannot handle the init-only members
/// on the Query record types, so bound types stay plain
/// </remarks>
public class LogRecordAnnotation
{
    public int Offset { get; set; }

    public int Length { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Page field the change updated, when it maps to a known field - used to name and style the value from the
    /// shared marker styles. Null for changes shown as a plain description
    /// </summary>
    public ItemType? ItemType { get; set; }

    /// <summary>
    /// New value for a field change
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Page structure region the change lands in - Header, Offset Table or Data
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Badge colour for the category, as a hex string
    /// </summary>
    public string CategoryColour { get; set; } = string.Empty;

    public bool HasField => ItemType is not null;

    public Visibility FieldVisibility => HasField ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DescriptionVisibility => HasField ? Visibility.Collapsed : Visibility.Visible;
}
