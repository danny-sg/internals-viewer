using InternalsViewer.Query.Events;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// A pointer-hit target recorded during a paint pass: the screen rect, the event it maps to, and an optional tooltip
/// label override
/// </summary>
/// <remarks>
/// Renderers append these to a single list the control owns and hit-tests in reverse (last drawn wins).
/// </remarks>
internal readonly record struct HitRegion(SKRect Bounds, EngineEvent Event, string? Label);
