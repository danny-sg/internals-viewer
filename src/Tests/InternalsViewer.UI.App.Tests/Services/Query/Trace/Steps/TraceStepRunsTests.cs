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

    private static void Append(ObservableCollection<AccessStep> history, AccessStep step)
        => TraceStepRuns.Append(step, history, HistoryLimit);
}
