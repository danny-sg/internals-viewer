using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Transactions;
using InternalsViewer.Query.Events.Waits;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Timeline.Definition;
using InternalsViewer.UI.App.Controls.Timeline;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Services.Query.Timeline;
using InternalsViewer.UI.App.ViewModels.Query;

namespace InternalsViewer.UI.App.Tests.Services.Query.Timeline;

[Trait("Category", "Unit")]
[Trait("Area", "Timeline")]
public class TimelineDefinitionBuilderTests
{
    private static readonly TimelineBandVisibility ShowAll = new(ShowLocks: true, ShowLatches: true, ShowWaits: true);

    [Fact]
    public void Shows_Plan_And_Read_Bands_Even_Without_Events()
    {
        var definition = Build([], ShowAll);

        Assert.Equal([typeof(ExecutionOperatorEvent), typeof(ReadEventGroup)], definition.Bands.Select(l => l.Key));
    }

    [Fact]
    public void Orders_Bands_Top_To_Bottom_And_Drops_Those_Without_Events()
    {
        var definition = Build([new WaitEvent(), new TransactionLogEvent(), new SegmentScanEvent()], ShowAll);

        Assert.Equal(["Log", "Plan", "Columnstore", "Read", "Wait"], definition.Bands.Select(l => l.Label));
    }

    [Fact]
    public void Drops_A_Hidden_Band_And_Leaves_Its_Events_Unplaced()
    {
        var definition = Build([new WaitEvent()], ShowAll with { ShowWaits = false });

        Assert.DoesNotContain(definition.Bands, l => l.Key == typeof(WaitEvent));
        Assert.Equal(-1, definition.Items[0].Band);
    }

    [Fact]
    public void Leaves_A_Lock_Group_Unplaced_But_Shows_The_Lock_Band_For_It()
    {
        var definition = Build([new LockGroup { Events = [] }], ShowAll);

        Assert.Contains(definition.Bands, l => l.Key == typeof(LockEvent));
        Assert.Equal(-1, definition.Items[0].Band);
    }

    [Fact]
    public void Places_Operators_And_Locks_Without_Drawing_A_Marker()
    {
        var operatorEvent = new ExecutionOperatorEvent { OperatorDescription = string.Empty };

        var lockEvent = new LockEvent { Resource = new LockResource() };

        var definition = Build([operatorEvent, lockEvent], ShowAll);

        Assert.All(definition.Items, i => Assert.True(i.Band >= 0));
        Assert.All(definition.Items, i => Assert.Equal(TimelineFill.None, i.Fill));
    }

    [Fact]
    public void Splits_Reads_Into_Cached_Above_And_Everything_Else_Below()
    {
        var cached = new ReadEventGroup { Events = [], ReadType = ReadType.Cached };

        var physical = new ReadEventGroup { Events = [], ReadType = ReadType.NonCached };

        var definition = Build([cached, physical, new IoEvent()], ShowAll);

        Assert.Equal(0, definition.Items[0].Track);
        Assert.Equal(1, definition.Items[1].Track);
        Assert.Equal(1, definition.Items[2].Track);
        Assert.All(definition.Items, i => Assert.Equal(2, i.TrackCount));
    }

    [Fact]
    public void Ticks_A_Read_Group_At_Its_End_And_Colours_It_By_The_Provider()
    {
        var definition = Build([new ReadEventGroup { Events = [] }, new IoEvent()], ShowAll);

        Assert.Equal(TimelineTickAnchor.End, definition.Items[0].Tick);
        Assert.Equal(TimelineTickAnchor.Start, definition.Items[1].Tick);
        Assert.All(definition.Items, i => Assert.Equal(TimelineColourSource.Provider, i.ColourSource));
    }

    [Fact]
    public void Steps_A_Categorised_Event_By_Its_Category_With_A_Fixed_Tint()
    {
        var definition = Build([new WaitEvent { Category = EventCategory.Parallelism }], ShowAll);

        var item = definition.Items[0];

        Assert.Equal((int)EventCategory.Parallelism, item.Track);
        Assert.Equal(EventCategoryClassifier.CategoryCount, item.TrackCount);
        Assert.Equal(TimelineColourSource.Fixed, item.ColourSource);
        Assert.Equal(TimelineColours.TintByCategory(definition.Bands[item.Band].Colour, 3), item.Colour);
    }

    [Fact]
    public void Fills_The_Band_For_An_Uncategorised_Event()
    {
        var definition = Build([new TransactionLogEvent()], ShowAll);

        var item = definition.Items[0];

        Assert.Equal((0, 1, (byte)0), (item.Track, item.TrackCount, item.Gap));
        Assert.Equal(TimelineColourSource.Provider, item.ColourSource);
    }

    [Fact]
    public void Stacks_Overlapping_Segment_Scans_In_The_Top_Half_As_Solid_Bars()
    {
        var definition = Build(
        [
            new SegmentScanEvent { RowGroupId = 0, ColumnId = 1, TimeUs = 100, DurationUs = 50 },
            new SegmentScanEvent { RowGroupId = 0, ColumnId = 2, TimeUs = 110, DurationUs = 50 },
        ], ShowAll);

        Assert.Equal((0, 4), (definition.Items[0].Track, definition.Items[0].TrackCount));
        Assert.Equal((1, 4), (definition.Items[1].Track, definition.Items[1].TrackCount));
        Assert.All(definition.Items, i => Assert.Equal(TimelineFill.Solid, i.Fill));
    }

    [Fact]
    public void Places_Object_Pool_Lookups_In_The_Bottom_Half()
    {
        var definition = Build(
        [
            new ObjectPoolEvent { IsHit = true, ColumnId = 1 },
            new ObjectPoolEvent { IsHit = false, ColumnId = 2 },
        ], ShowAll);

        Assert.Equal((2, 4), (definition.Items[0].Track, definition.Items[0].TrackCount));
        Assert.Equal((3, 4), (definition.Items[1].Track, definition.Items[1].TrackCount));
        Assert.Equal(4f, definition.Items[0].MinWidth);
        Assert.Equal(0f, definition.Items[1].MinWidth);
    }

    [Fact]
    public void Spans_A_Rowgroup_Event_Across_The_Top_Half_And_Other_Scan_Events_Across_The_Bottom()
    {
        var definition = Build(
        [
            new SegmentEliminateEvent(),
            new ColumnStoreScanEvent { EventName = "column_store_rowgroup_read_issued" },
            new ColumnStoreScanEvent { EventName = "column_store_fast_string_equals" },
        ], ShowAll);

        Assert.Equal([0, 0, 1], definition.Items.Select(i => i.Track));
        Assert.All(definition.Items, i => Assert.Equal(2, i.TrackCount));
    }

    [Fact]
    public void Holds_The_Columnstore_Band_At_A_Height_That_Fits_Its_Widest_Stack()
    {
        var definition = Build(
        [
            new SegmentScanEvent { RowGroupId = 0, ColumnId = 1, TimeUs = 100, DurationUs = 50 },
            new SegmentScanEvent { RowGroupId = 0, ColumnId = 2, TimeUs = 110, DurationUs = 50 },
            new SegmentScanEvent { RowGroupId = 0, ColumnId = 3, TimeUs = 120, DurationUs = 50 },
        ], ShowAll);

        var band = definition.Bands.Single(l => l.Key == typeof(SegmentScanEvent));

        Assert.Equal(3 * SegmentScanTracks.MinTrackHeight * 2, band.MinInnerHeight);
    }

    [Fact]
    public void Links_A_Read_To_Its_Object_Pool_Lookup_And_Its_Operator()
    {
        var node = new PlanNodeIdentifier(1, 1);

        var lookup = new ObjectPoolEvent();

        var definition = Build(
        [
            new ReadEventGroup { Events = [], PoolLookup = lookup, PlanNodeIdentifier = node },
            lookup,
            new ReadEventGroup { Events = [], PlanNodeIdentifier = node },
        ], ShowAll);

        Assert.Equal(
        [
            new TimelineLink(0, 1, node, TimelineLinkStyle.CallReturn, 1),
            new TimelineLink(2, -1, node, TimelineLinkStyle.CallReturn, 1),
        ], definition.Links);
    }

    [Fact]
    public void Leaves_A_Read_Unlinked_When_Its_Lookup_Is_Off_The_Timeline_And_It_Has_No_Operator()
    {
        var definition = Build(
        [
            new ReadEventGroup { Events = [], PoolLookup = new ObjectPoolEvent() },
            new ReadEventGroup { Events = [] },
            new IoEvent { PlanNodeIdentifier = new PlanNodeIdentifier(1, 1) },
        ], ShowAll);

        Assert.Empty(definition.Links);
    }

    [Fact]
    public void Gives_A_Read_One_Return_Rail_Per_Page()
    {
        var read = new ReadEventGroup
        {
            Events = [],
            PlanNodeIdentifier = new PlanNodeIdentifier(1, 1),
            Pages = [new PageAddress(1, 10), new PageAddress(1, 11), new PageAddress(1, 12)],
        };

        var definition = Build([read], ShowAll);

        Assert.Equal(3, Assert.Single(definition.Links).RailCount);
    }

    [Fact]
    public void Links_A_Log_Record_To_Its_Operator_As_A_Bar()
    {
        var node = new PlanNodeIdentifier(1, 1);

        var definition = Build([new TransactionLogEvent { PlanNodeIdentifier = node }, new TransactionLogEvent()], ShowAll);

        Assert.Equal([new TimelineLink(0, -1, node, TimelineLinkStyle.Bar, 1)], definition.Links);
    }

    [Fact]
    public void Splits_The_Object_Pool_Sub_Band_Into_Segment_And_Dictionary_Lanes_With_A_Divider_Above_Each()
    {
        var definition = Build(
        [
            new ObjectPoolEvent { ObjectType = ColumnStoreObjectType.ColumnSegment, RowGroupId = 1, ColumnId = 2, TimeUs = 0 },
            new ObjectPoolEvent { ObjectType = ColumnStoreObjectType.ColumnSegment, RowGroupId = 0, ColumnId = 2, TimeUs = 100 },
            new ObjectPoolEvent { ObjectType = ColumnStoreObjectType.PrimaryDictionary, ColumnId = 7, TimeUs = 200 },
        ], ShowAll);

        Assert.Equal([(2, 4), (2, 4), (3, 4)], definition.Items.Select(i => (i.Track, i.TrackCount)));

        var band = definition.Bands.Single(b => b.Key == typeof(SegmentScanEvent));

        Assert.Equal([new TimelineTrackDivider(2, 4), new TimelineTrackDivider(3, 4)], band.TrackDividers);
    }

    [Fact]
    public void Links_A_Read_Ahead_To_The_Lookup_It_Fetched_For()
    {
        var node = new PlanNodeIdentifier(1, 1);

        var lookup = new ObjectPoolEvent();

        var definition = Build(
        [
            new ReadEventGroup { Events = [], IsReadAhead = true, PoolLookup = lookup, PlanNodeIdentifier = node },
            lookup,
        ], ShowAll);

        Assert.Equal([new TimelineLink(0, 1, node, TimelineLinkStyle.CallReturn, 1)], definition.Links);
    }

    [Fact]
    public void Expands_Read_Group_Members_Except_File_Events_And_Orders_By_Sequence()
    {
        var io = new IoEvent { SequenceId = 3 };

        var file = new FileEvent { SequenceId = 4 };

        var group = new ReadEventGroup { Events = [io, file], SequenceId = 2 };

        var wait = new WaitEvent { SequenceId = 1 };

        var definition = Build([group, wait], ShowAll);

        Assert.Equal<EngineEvent>([wait, group, io], definition.Events);
        Assert.Equal(definition.Events.Count, definition.Items.Length);
    }

    [Fact]
    public void Colours_An_Allocation_Page_Read_With_A_Fixed_Colour()
    {
        var definition = Build([new ReadEventGroup { Events = [], IsAllocationPage = true }, new ReadEventGroup { Events = [] }], ShowAll);

        Assert.Equal(TimelineColourSource.Fixed, definition.Items[0].ColourSource);
        Assert.Equal(ColourConstants.AllocationPageColour.ToSkColor(), definition.Items[0].Colour);
        Assert.Equal(TimelineColourSource.Provider, definition.Items[1].ColourSource);
    }

    [Fact]
    public void Starts_A_Segment_Scan_After_A_Hit_Tick_At_The_Same_Instant()
    {
        var definition = Build([new SegmentScanEvent(), new ObjectPoolEvent { IsHit = true }], ShowAll);

        Assert.Equal(definition.Items[1].MinWidth, definition.Items[0].StartInset);
    }

    [Fact]
    public void Links_Nothing_From_An_Allocation_Page_Read()
    {
        var lookup = new ObjectPoolEvent();

        var read = new ReadEventGroup
        {
            Events = [],
            IsAllocationPage = true,
            PoolLookup = lookup,
            PlanNodeIdentifier = new PlanNodeIdentifier(1,
            1),
        };

        var definition = Build([read, lookup], ShowAll);

        Assert.Empty(definition.Links);
    }

    [Fact]
    public void Leaves_Out_Events_The_Visibility_Filter_Rejects()
    {
        var wait = new WaitEvent();

        var read = new ReadEventGroup { Events = [] };

        var definition = TimelineDefinitionBuilder.CreateDefault().Build([wait, read], ShowAll, e => e is not WaitEvent);

        Assert.Equal<EngineEvent>([read], definition.Events);
    }

    private static TimelineDefinition Build(EngineEvent[] events, TimelineBandVisibility visibility)
        => TimelineDefinitionBuilder.CreateDefault().Build(events, visibility);
}
