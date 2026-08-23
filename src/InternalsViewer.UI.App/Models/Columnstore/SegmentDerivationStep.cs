using SkiaSharp;
using Windows.UI;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// One token of the chain turning a stored value into the column's value
/// </summary>
/// <remarks>
/// A token carries a chip for what it is, and where it applies a constant, the split badge naming the field that
/// constant came from. A token with no prefix follows the one before it as an alternative rather than a next step.
/// </remarks>
public sealed record SegmentDerivationStep
{
    public string Prefix { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public Color Background { get; init; }

    public Color Foreground { get; init; }

    /// <summary>
    /// Outline, which only the white chip needs since the box behind it is white too
    /// </summary>
    public Color Border { get; init; }

    public string Operator { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string Suffix { get; init; } = string.Empty;

    /// <summary>
    /// Where the constant was read from, which is not always the blob
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// Value half of the split badge, coloured by whether the constant came from the blob or the metadata
    /// </summary>
    public Color BadgeBackground { get; init; } = BlobConstant;

    public bool HasPrefix => Prefix.Length > 0;

    public bool HasChip => Text.Length > 0;

    public bool HasOperator => Operator.Length > 0;

    public bool HasBadge => Name.Length > 0;

    public bool HasSuffix => Suffix.Length > 0;

    public static Color FromSkia(SKColor colour) => Color.FromArgb(255, colour.Red, colour.Green, colour.Blue);

    public static Color White => Color.FromArgb(255, 255, 255, 255);

    public static Color Black => Color.FromArgb(255, 0, 0, 0);

    public static Color Outline => Color.FromArgb(255, 190, 190, 190);

    public static Color BlobConstant => Color.FromArgb(255, 31, 111, 235);

    public static Color MetadataConstant => Color.FromArgb(255, 126, 87, 194);
}
