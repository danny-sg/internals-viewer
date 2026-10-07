using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.UI.App.Services.Query.Timeline;

namespace InternalsViewer.UI.App.Tests.Services.Query.Timeline;

[Trait("Category", "Unit")]
[Trait("Area", "Timeline")]
public class SegmentEliminationTracksTests
{
    [Fact]
    public void Gives_Each_Rowgroup_In_A_Run_Of_Eliminations_Its_Own_Track()
    {
        var tracks = new SegmentEliminationTracks();

        tracks.Rebuild([Eliminate(5), Eliminate(4), Eliminate(3)]);

        Assert.Equal(3, tracks.TrackCount);
        Assert.Equal([0, 1, 2], Enumerable.Range(0, 3).Select(tracks.TrackOf));
        Assert.All(Enumerable.Range(0, 3), i => Assert.Equal(3, tracks.TrackCountOf(i)));
    }

    [Fact]
    public void Leaves_A_Lone_Elimination_On_A_Single_Track()
    {
        var tracks = new SegmentEliminationTracks();

        tracks.Rebuild([Eliminate(5)]);

        Assert.Equal(1, tracks.TrackCount);
        Assert.Equal(0, tracks.TrackOf(0));
        Assert.Equal(1, tracks.TrackCountOf(0));
    }

    [Fact]
    public void Starts_A_New_Run_After_The_Task_Scans_A_Rowgroup()
    {
        var tracks = new SegmentEliminationTracks();

        tracks.Rebuild(
        [
            Eliminate(5),
            Eliminate(4),
            new SegmentScanEvent { RowGroupId = 3, TaskAddress = 7 },
            Eliminate(2),
        ]);

        Assert.Equal(2, tracks.TrackCount);
        Assert.Equal((0, 2), (tracks.TrackOf(0), tracks.TrackCountOf(0)));
        Assert.Equal((1, 2), (tracks.TrackOf(1), tracks.TrackCountOf(1)));
        Assert.Equal((0, 1), (tracks.TrackOf(3), tracks.TrackCountOf(3)));
    }

    [Fact]
    public void Keeps_A_Run_Going_Through_Other_Events_And_Other_Tasks_Scans()
    {
        var tracks = new SegmentEliminationTracks();

        tracks.Rebuild(
        [
            Eliminate(5),
            new ObjectPoolEvent { TaskAddress = 7 },
            new SegmentScanEvent { RowGroupId = 9, TaskAddress = 8 },
            Eliminate(4),
        ]);

        Assert.Equal((0, 2), (tracks.TrackOf(0), tracks.TrackCountOf(0)));
        Assert.Equal((1, 2), (tracks.TrackOf(3), tracks.TrackCountOf(3)));
    }

    [Fact]
    public void Runs_Are_Separate_Per_Task_And_Per_Hobt()
    {
        var tracks = new SegmentEliminationTracks();

        tracks.Rebuild(
        [
            Eliminate(5),
            Eliminate(5, task: 8),
            Eliminate(4),
            Eliminate(4, hobt: 2),
        ]);

        Assert.Equal((0, 2), (tracks.TrackOf(0), tracks.TrackCountOf(0)));
        Assert.Equal((0, 1), (tracks.TrackOf(1), tracks.TrackCountOf(1)));
        Assert.Equal((1, 2), (tracks.TrackOf(2), tracks.TrackCountOf(2)));
        Assert.Equal((0, 1), (tracks.TrackOf(3), tracks.TrackCountOf(3)));
    }

    private static SegmentEliminateEvent Eliminate(long rowGroup, ulong task = 7, ulong hobt = 1) => new()
    {
        RowGroupId = rowGroup,
        TaskAddress = task,
        HobtId = hobt,
    };
}
