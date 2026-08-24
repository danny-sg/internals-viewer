namespace InternalsViewer.UI.App.Models.Columnstore.Segment;

/// <summary>
/// A place in the segment blob, being the region holding it and the offset the item starts at
/// </summary>
public sealed record SegmentNavigationTarget(SegmentRegion Region, int Offset);
