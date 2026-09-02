using System.Collections.ObjectModel;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.UI.App.Models.Query.Trace.Steps;
using InternalsViewer.UI.App.Services.Query.Trace.Steps;

namespace InternalsViewer.UI.App.Tests.Services.Query.Trace.Steps;

[Trait("Category", "Unit")]
[Trait("Area", "Trace")]
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

        var span = Single(history, "Accumulate");

        Assert.Equal(5, span.Number("Rows"));

        Assert.Equal("COUNT(*) = 5", Text(span, "Values"));

        Assert.DoesNotContain(history, s => s is AccessStep.AggregateRow);
    }

    [Fact]
    public void Emitting_A_Group_Closes_Its_Span_So_The_Next_Group_Starts_A_New_One()
    {
        var history = new ObservableCollection<AccessStep>();

        Append(history, new AccessStep.AggregateRow(1, 1) { NodeId = 1 });
        Append(history, new AccessStep.AggregateEmit(1, "20") { NodeId = 1, GroupRows = 1 });

        Assert.True(Single(history, "Accumulate").IsComplete);

        Append(history, new AccessStep.AggregateRow(2, 1) { NodeId = 1 });

        Assert.Equal(2, Spans(history, "Accumulate").Count);

        Assert.Single(Spans(history, "Accumulate").Where(s => !s.IsComplete));
    }

    [Fact]
    public void A_Span_Is_Kept_Per_Operator()
    {
        var history = new ObservableCollection<AccessStep>();

        Append(history, new AccessStep.AggregateRow(1, 1) { NodeId = 1 });
        Append(history, new AccessStep.AggregateRow(1, 1) { NodeId = 2 });
        Append(history, new AccessStep.AggregateRow(2, 2) { NodeId = 1 });

        var spans = Spans(history, "Accumulate");

        Assert.Equal(2, spans.Count);

        Assert.Equal(2, spans.Single(s => s.NodeId == 1).Number("Rows"));

        Assert.Equal(1, spans.Single(s => s.NodeId == 2).Number("Rows"));
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

        var span = Single(history, "Accumulate");

        Assert.Equal(6, span.Number("Rows"));

        Assert.Equal(3, span.Number("Groups"));

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

        var span = Single(history, "Accumulate");

        Assert.True(span.Fill.HasBuckets);

        Assert.Equal([1, 2, 0, 1], span.Fill.Buckets);

        Assert.Equal(3, span.Number("Bucket"));
    }

    [Fact]
    public void A_Stream_Aggregate_Span_Is_Not_Hashed()
    {
        var history = new ObservableCollection<AccessStep>();

        Append(history, new AccessStep.AggregateRow(1, 1) { NodeId = 1, Running = "COUNT(*) = 1" });

        Assert.False(Single(history, "Accumulate").Fill.HasBuckets);
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

        var output = Single(history, "Get Row");

        Assert.Equal(3, output.Number("Row"));

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

        Assert.Equal(2, Spans(history, "Accumulate").Count);

        Assert.All(Spans(history, "Accumulate"), span => Assert.True(span.IsComplete));
    }

    [Fact]
    public void Computed_Rows_Fold_Into_A_Row_Count_Span()
    {
        var history = new ObservableCollection<AccessStep>();

        for (var number = 1; number <= 4; number++)
        {
            Append(history, new AccessStep.ComputeRow(number) { NodeId = 3 });
        }

        Assert.Equal(4, Single(history, "Get Row").Number("Row"));

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
                Key = $"{20 + ((number - 1) / 5)}"
            });
        }

        var span = Single(history, "Segment");

        Assert.Equal(15, span.Number("Rows"));

        Assert.Equal(3, span.Number("Segments"));

        Assert.Equal("22", Text(span, "Key"));

        Assert.DoesNotContain(history, s => s is AccessStep.SegmentRow);
    }

    [Fact]
    public void Ranked_Rows_Fold_Into_One_Span()
    {
        var history = new ObservableCollection<AccessStep>();

        for (var number = 1; number <= 15; number++)
        {
            Append(history, new AccessStep.RankRow(number)
            {
                NodeId = 5,
                IsNewPartition = number % 5 == 1,
                Values = $"Expr1002 = {((number - 1) % 5) + 1}"
            });
        }

        var span = Single(history, "Rank");

        Assert.Equal(15, span.Number("Row"));

        Assert.Equal("Expr1002 = 5", Text(span, "Values"));

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

        Assert.Equal(2, Single(history, "Segment").Number("Rows"));

        Assert.Equal(2, Single(history, "Rank").Number("Row"));
    }

    [Fact]
    public void A_Counter_Is_Created_Once_And_Then_Mutated()
    {
        var history = new ObservableCollection<AccessStep>();

        for (var number = 1; number <= 50; number++)
        {
            Append(history, new AccessStep.ComputeRow(number) { NodeId = 3 });
        }

        var span = Single(history, "Get Row");

        Assert.Equal(2, span.Items.Count);

        Assert.Equal(50, span.Number("Row"));
    }

    private static List<TraceCounterSpan> Spans(ObservableCollection<AccessStep> history, string label)
        => [.. history.OfType<TraceCounterSpan>().Where(s => s.Label == label)];

    private static TraceCounterSpan Single(ObservableCollection<AccessStep> history, string label)
        => Assert.Single(Spans(history, label));

    private static string Text(TraceCounterSpan span, string name)
        => span.Items.Single(c => c.Name == name).Text;

    private static void Append(ObservableCollection<AccessStep> history, AccessStep step)
        => TraceStepRuns.Append(step, history, HistoryLimit);
}
