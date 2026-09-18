using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.UI.App.Services.Query.Timeline;

namespace InternalsViewer.UI.App.Tests.Services.Query.Timeline;

[Trait("Category", "Unit")]
[Trait("Area", "Timeline")]
public class ObjectPoolTracksTests
{
    [Fact]
    public void Shares_Tracks_Between_Rowgroups_Loaded_One_After_Another()
    {
        var tracks = new ObjectPoolTracks();

        tracks.Rebuild(
        [
            Lookup(ColumnStoreObjectType.ColumnSegment, rowGroup: 1, column: 2, timeUs: 0, durationUs: 10),
            Lookup(ColumnStoreObjectType.ColumnSegment, rowGroup: 1, column: 3, timeUs: 10, durationUs: 10),
            Lookup(ColumnStoreObjectType.ColumnSegment, rowGroup: 0, column: 2, timeUs: 100, durationUs: 10),
            Lookup(ColumnStoreObjectType.ColumnSegment, rowGroup: 0, column: 3, timeUs: 110, durationUs: 10),
        ]);

        Assert.Equal(2, tracks.TrackCount);
        Assert.Equal([0, 1, 0, 1], Enumerable.Range(0, 4).Select(tracks.TrackOf));
    }

    [Fact]
    public void Splits_Rowgroups_Loaded_In_Parallel_Onto_Their_Own_Tracks()
    {
        var tracks = new ObjectPoolTracks();

        tracks.Rebuild(
        [
            Lookup(ColumnStoreObjectType.ColumnSegment, rowGroup: 1, column: 2, timeUs: 0, durationUs: 50),
            Lookup(ColumnStoreObjectType.ColumnSegment, rowGroup: 0, column: 2, timeUs: 20, durationUs: 50),
            Lookup(ColumnStoreObjectType.ColumnSegment, rowGroup: 2, column: 2, timeUs: 60, durationUs: 50),
        ]);

        Assert.Equal(2, tracks.TrackCount);
        Assert.Equal([0, 1, 0], Enumerable.Range(0, 3).Select(tracks.TrackOf));
    }

    [Fact]
    public void Puts_Dictionaries_In_Their_Own_Lane_Below_The_Segments()
    {
        var tracks = new ObjectPoolTracks();

        tracks.Rebuild(
        [
            Lookup(ColumnStoreObjectType.PrimaryDictionary, rowGroup: null, column: 7, timeUs: 0, durationUs: 5),
            Lookup(ColumnStoreObjectType.ColumnSegment, rowGroup: 1, column: 2, timeUs: 10, durationUs: 5),
            Lookup(ColumnStoreObjectType.DeleteBitmap, rowGroup: 1, column: -1, timeUs: 20, durationUs: 0),
            Lookup(ColumnStoreObjectType.SecondaryDictionary, rowGroup: 1, column: 8, timeUs: 30, durationUs: 5),
        ]);

        Assert.Equal(4, tracks.TrackCount);
        Assert.Equal([2, 0, 1, 3], Enumerable.Range(0, 4).Select(tracks.TrackOf));
        Assert.Equal([0, 2], tracks.LaneStarts);
    }

    [Fact]
    public void Keeps_A_Hit_And_A_Miss_For_The_Same_Object_On_One_Track()
    {
        var tracks = new ObjectPoolTracks();

        tracks.Rebuild(
        [
            Lookup(ColumnStoreObjectType.ColumnSegment, rowGroup: 0, column: 2, timeUs: 0, durationUs: 10),
            Lookup(ColumnStoreObjectType.ColumnSegment, rowGroup: 0, column: 2, timeUs: 20, durationUs: 0, isHit: true),
        ]);

        Assert.Equal(1, tracks.TrackCount);
        Assert.Equal(tracks.TrackOf(0), tracks.TrackOf(1));
    }

    [Fact]
    public void Skips_Events_That_Are_Not_Pool_Lookups()
    {
        var tracks = new ObjectPoolTracks();

        tracks.Rebuild([new SegmentScanEvent(), Lookup(ColumnStoreObjectType.ColumnSegment, rowGroup: 0, column: 2, timeUs: 0, durationUs: 0)]);

        Assert.Equal(1, tracks.TrackCount);
        Assert.Equal(0, tracks.TrackOf(1));
        Assert.Equal([0], tracks.LaneStarts);
    }

    private static EngineEvent Lookup(ColumnStoreObjectType type, long? rowGroup, int column, long timeUs, long durationUs, bool isHit = false)
        => new ObjectPoolEvent
        {
            ObjectType = type,
            RowGroupId = rowGroup,
            ColumnId = column,
            TimeUs = timeUs,
            DurationUs = durationUs,
            IsHit = isHit,
        };
}
