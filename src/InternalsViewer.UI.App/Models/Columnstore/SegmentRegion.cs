namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// Region of the blob a tab shows, which drives both the hex window and the markers built for it
/// </summary>
public enum SegmentRegion
{
    Header,
    Bookmarks,
    RleArray,
    BitpackArray,
    ValueStore
}
