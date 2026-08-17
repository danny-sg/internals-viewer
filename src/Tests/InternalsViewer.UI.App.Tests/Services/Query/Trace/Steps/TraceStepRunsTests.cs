using System.Collections.ObjectModel;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.UI.App.Models.Query.Trace.Steps;
using InternalsViewer.UI.App.Services.Query.Trace.Steps;

namespace InternalsViewer.UI.App.Tests.Services.Query.Trace.Steps;

public class TraceStepRunsTests
{
    private const int HistoryLimit = 1000;

    [Fact]
    public void Accumulated_Rows_Fold_Into_One_Span()
    {
        var history = new ObservableCollection<AccessStep>();

        Append(history, new AccessStep.AggregateGroup(1, "") { NodeId = 1 });

        for (var number = 1; number <= 5; number++)
        {
            Append(history, new AccessStep.AggregateRow(number, number) { NodeId = 1, Running = $"COUNT(*) = {number}" });
        }

        var span = Assert.Single(history.OfType<StreamAggregateSpan>());

        Assert.Equal(5, span.Progress.Rows);
        Assert.Equal(5, span.Progress.GroupRows);
        Assert.Equal("COUNT(*) = 5", span.Progress.Running);

        Assert.DoesNotContain(history, s => s is AccessStep.AggregateRow);
    }

    [Fact]
    public void Emitting_A_Group_Closes_Its_Span_So_The_Next_Group_Starts_A_New_One()
    {
        var history = new ObservableCollection<AccessStep>();

        Append(history, new AccessStep.AggregateRow(1, 1) { NodeId = 1 });
        Append(history, new AccessStep.AggregateEmit(1, "20") { NodeId = 1, GroupRows = 1 });

        var first = Assert.Single(history.OfType<StreamAggregateSpan>());

        Assert.True(first.IsComplete);

        Append(history, new AccessStep.AggregateRow(2, 1) { NodeId = 1 });

        Assert.Equal(2, history.OfType<StreamAggregateSpan>().Count());

        Assert.Single(history.OfType<StreamAggregateSpan>().Where(s => !s.IsComplete));
    }

    [Fact]
    public void A_Span_Is_Kept_Per_Operator()
    {
        var history = new ObservableCollection<AccessStep>();

        Append(history, new AccessStep.AggregateRow(1, 1) { NodeId = 1 });
        Append(history, new AccessStep.AggregateRow(1, 1) { NodeId = 2 });
        Append(history, new AccessStep.AggregateRow(2, 2) { NodeId = 1 });

        var spans = history.OfType<StreamAggregateSpan>().ToList();

        Assert.Equal(2, spans.Count);

        Assert.Equal(2, spans.Single(s => s.NodeId == 1).Progress.GroupRows);
        Assert.Equal(1, spans.Single(s => s.NodeId == 2).Progress.GroupRows);
    }

    [Fact]
    public void Hashed_Rows_And_New_Groups_Both_Fold_Into_One_Span()
    {
        var history = new ObservableCollection<AccessStep>();

        for (var number = 1; number <= 6; number++)
        {
            Append(history, new AccessStep.HashAggregate(number % 3, 0, 0)
            {
                NodeId = 1,
                Number = number,
                IsNewGroup = number <= 3,
                Running = $"COUNT(*) = {number}"
            });
        }

        var span = Assert.Single(history.OfType<StreamAggregateSpan>());

        Assert.Equal(6, span.Progress.Rows);
        Assert.Equal(3, span.Progress.Groups);
        Assert.Equal("3 groups", span.Progress.Detail);

        Assert.DoesNotContain(history, s => s is AccessStep.HashAggregate);
    }

    [Fact]
    public void A_Hashed_Span_Tracks_The_Bucket_Fill()
    {
        var history = new ObservableCollection<AccessStep>();

        // (bucket, chain length after this row landed in it)
        (int Bucket, int ChainLength)[] landings = [(1, 1), (0, 1), (1, 2), (3, 1)];

        for (var index = 0; index < landings.Length; index++)
        {
            Append(history, new AccessStep.HashAggregate(landings[index].Bucket, 0, 0)
            {
                NodeId = 1,
                Number = index + 1,
                BucketCount = 4,
                ChainLength = landings[index].ChainLength,
                IsNewGroup = landings[index].ChainLength == 1
            });
        }

        var span = Assert.Single(history.OfType<StreamAggregateSpan>());

        Assert.True(span.Progress.IsHashed);

        Assert.Equal([1, 2, 0, 1], span.Progress.Fill);

        Assert.Equal(3, span.Progress.Bucket);
    }

    [Fact]
    public void A_Stream_Aggregate_Span_Is_Not_Hashed()
    {
        var history = new ObservableCollection<AccessStep>();

        Append(history, new AccessStep.AggregateRow(1, 1) { NodeId = 1, Running = "COUNT(*) = 1" });

        var span = Assert.Single(history.OfType<StreamAggregateSpan>());

        Assert.False(span.Progress.IsHashed);
        Assert.Empty(span.Progress.Fill);
    }

    [Fact]
    public void Output_Rows_Fold_Into_One_Span_Across_Every_Group()
    {
        var history = new ObservableCollection<AccessStep>();

        Append(history, new AccessStep.AggregateRow(1, 1) { NodeId = 1 });
        Append(history, new AccessStep.AggregateEmit(1, "20") { NodeId = 1, GroupRows = 1 });

        Append(history, new AccessStep.AggregateRow(2, 1) { NodeId = 1 });
        Append(history, new AccessStep.AggregateEmit(2, "21") { NodeId = 1, GroupRows = 1 });

        Append(history, new AccessStep.AggregateRow(3, 1) { NodeId = 1 });
        Append(history, new AccessStep.AggregateEmit(3, "22") { NodeId = 1, GroupRows = 1 });

        var output = Assert.Single(history.OfType<RowCountSpan>());

        Assert.Equal(3, output.Progress.Rows);
        Assert.False(output.IsComplete);

        Assert.DoesNotContain(history, s => s is AccessStep.AggregateEmit);
    }

    [Fact]
    public void An_Output_Span_Does_Not_Swallow_The_Accumulate_Spans()
    {
        var history = new ObservableCollection<AccessStep>();

        Append(history, new AccessStep.AggregateRow(1, 1) { NodeId = 1 });
        Append(history, new AccessStep.AggregateEmit(1, "20") { NodeId = 1 });
        Append(history, new AccessStep.AggregateRow(2, 1) { NodeId = 1 });
        Append(history, new AccessStep.AggregateEmit(2, "21") { NodeId = 1 });

        Assert.Equal(2, history.OfType<StreamAggregateSpan>().Count());

        Assert.All(history.OfType<StreamAggregateSpan>(), span => Assert.True(span.IsComplete));
    }

    [Fact]
    public void Computed_Rows_Fold_Into_A_Row_Count_Span()
    {
        var history = new ObservableCollection<AccessStep>();

        for (var number = 1; number <= 4; number++)
        {
            Append(history, new AccessStep.ComputeRow(number) { NodeId = 3 });
        }

        var span = Assert.Single(history.OfType<RowCountSpan>());

        Assert.Equal(4, span.Progress.Rows);

        Assert.DoesNotContain(history, s => s is AccessStep.ComputeRow);
    }

    [Fact]
    public void Segmented_Rows_Fold_Into_One_Span_Counting_Segments()
    {
        var history = new ObservableCollection<AccessStep>();

        for (var number = 1; number <= 15; number++)
        {
            var isNewSegment = number % 5 == 1;

            Append(history, new AccessStep.SegmentRow(number, isNewSegment)
            {
                NodeId = 4,
                SegmentCount = (number + 4) / 5,
                Key = $"{20 + (number - 1) / 5}"
            });
        }

        var span = Assert.Single(history.OfType<SegmentSpan>());

        Assert.Equal(15, span.Progress.Rows);
        Assert.Equal(3, span.Progress.Segments);
        Assert.Equal("22", span.Progress.Key);
        Assert.True(span.Progress.HasKey);

        Assert.DoesNotContain(history, s => s is AccessStep.SegmentRow);
    }

    [Fact]
    public void A_Segment_With_No_Grouping_Columns_Has_No_Key_To_Show()
    {
        var history = new ObservableCollection<AccessStep>();

        Append(history, new AccessStep.SegmentRow(1, true) { NodeId = 4, SegmentCount = 1 });

        var span = Assert.Single(history.OfType<SegmentSpan>());

        Assert.False(span.Progress.HasKey);
    }

    [Fact]
    public void Ranked_Rows_Fold_Into_One_Span_Counting_Partitions()
    {
        var history = new ObservableCollection<AccessStep>();

        for (var number = 1; number <= 15; number++)
        {
            Append(history, new AccessStep.RankRow(number)
            {
                NodeId = 5,
                IsNewPartition = number % 5 == 1,
                Values = $"Expr1002 = {(number - 1) % 5 + 1}"
            });
        }

        var span = Assert.Single(history.OfType<RankSpan>());

        Assert.Equal(15, span.Progress.Rows);
        Assert.Equal(3, span.Progress.Partitions);
        Assert.Equal("Expr1002 = 5", span.Progress.Values);

        Assert.DoesNotContain(history, s => s is AccessStep.RankRow);
    }

    [Fact]
    public void A_Segment_And_The_Sequence_Project_Above_It_Keep_Separate_Spans()
    {
        var history = new ObservableCollection<AccessStep>();

        Append(history, new AccessStep.SegmentRow(1, true) { NodeId = 4, SegmentCount = 1 });
        Append(history, new AccessStep.RankRow(1) { NodeId = 5 });
        Append(history, new AccessStep.SegmentRow(2, false) { NodeId = 4, SegmentCount = 1 });
        Append(history, new AccessStep.RankRow(2) { NodeId = 5 });

        Assert.Equal(2, Assert.Single(history.OfType<SegmentSpan>()).Progress.Rows);
        Assert.Equal(2, Assert.Single(history.OfType<RankSpan>()).Progress.Rows);
    }

    private static void Append(ObservableCollection<AccessStep> history, AccessStep step)
        => TraceStepRuns.Append(step, history, HistoryLimit);
}
