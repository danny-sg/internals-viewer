using System.Collections.Generic;
using SkiaSharp;
using Windows.UI;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// A labelled chip for the segment header, carrying its own colour
/// </summary>
/// <remarks>
/// The colours are the ones the structure drawing uses, so a segment reads the same in the header as it does in the
/// picture. They are held as Skia colours there, which is why the conversion lives here rather than in a converter.
/// </remarks>
public sealed class SegmentBadge
{
    public required string Label { get; init; }

    public required Color Background { get; init; }

    /// <summary>
    /// Rounded on the outer edges only, so a run of badges reads as one compound chip
    /// </summary>
    public CornerRadius CornerRadius { get; set; } = new(Radius);

    public static SegmentBadge  Create(string label, SKColor colour)
        => new() { Label = label, Background = Color.FromArgb(255, colour.Red, colour.Green, colour.Blue) };

    /// <summary>
    /// Joins a run of badges up, the corners being what tells one compound chip from the next
    /// </summary>
    public static IReadOnlyList<SegmentBadge> Compound(IReadOnlyList<SegmentBadge> badges)
    {
        for (var i = 0; i < badges.Count; i++)
        {
            var start = i == 0 ? Radius : 0;

            var end = i == badges.Count - 1 ? Radius : 0;

            badges[i].CornerRadius = new CornerRadius(start, end, end, start);
        }

        return badges;
    }

    private const double Radius = 3;
}
