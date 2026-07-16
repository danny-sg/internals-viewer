using System;
using InternalsViewer.Query.Events.Locks;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// Colour maths and event-to-colour mappings shared by the timeline renderers
/// </summary>
internal static class TimelineColours
{
    // Per-category brightness applied to the row colour so each category band reads slightly differently.
    private static readonly float[] CategoryShade = [0.70f, 0.85f, 1.0f, 1.15f];

    /// <summary>Scales a colour's RGB channels by <paramref name="factor"/> (clamped), preserving alpha.</summary>
    public static SKColor Scale(SKColor colour, float factor) => new(
        (byte)Math.Clamp(colour.Red * factor, 0, 255),
        (byte)Math.Clamp(colour.Green * factor, 0, 255),
        (byte)Math.Clamp(colour.Blue * factor, 0, 255),
        colour.Alpha);

    public static SKColor TintByCategory(SKColor colour, int category) => Scale(colour, CategoryShade[category]);

    /// <summary>
    /// Colour for a lock mode, by its broad category — the second dimension of the lock display (the lane is the first)
    /// </summary>
    public static SKColor LockModeColour(LockMode mode) => LockModeClassifier.Categorise(mode) switch
    {
        LockModeCategory.Read => new SKColor(76, 175, 80),    // green  — shared / intent-shared reads
        LockModeCategory.Update => new SKColor(255, 179, 0),  // amber  — update family (may escalate to a write)
        LockModeCategory.Write => new SKColor(229, 57, 53),   // red    — exclusive / intent-exclusive writes
        LockModeCategory.Schema => new SKColor(156, 39, 176), // purple — schema modification
        LockModeCategory.Range => new SKColor(33, 150, 243),  // blue   — serializable key-range protection
        LockModeCategory.Bulk => new SKColor(0, 150, 136),    // teal   — bulk update
        _ => new SKColor(120, 120, 120)                       // grey   — no / unknown mode
    };

    // Lock escalation granularity: row (rid/key) at the bottom, page in the middle, object (object/hobt) at the top.
    public static int GranularityLevel(LockResourceType resourceType) => resourceType switch
    {
        LockResourceType.Rid or LockResourceType.Key => 0,
        LockResourceType.Page or LockResourceType.Extent => 1,
        _ => 2,
    };

    // Escalation/exclusivity rank of a lock category — the higher this is, the higher its band sits on the lock lane,
    // so an escalation to a coarser, more exclusive lock (e.g. range/update keys -> an exclusive object lock) steps UP.
    public static int LockCategoryLevel(LockModeCategory category) => category switch
    {
        LockModeCategory.Schema => 5,
        LockModeCategory.Write => 4,
        LockModeCategory.Update => 3,
        LockModeCategory.Range => 2,
        LockModeCategory.Bulk => 1,
        _ => 0, // Read / None
    };
}
