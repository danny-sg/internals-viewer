using System.Data;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Executors;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.Tests.UnitTests.AccessPaths;

public class IndexSeekExecutorTests
{
    [Fact]
    public void Seek_Enters_Leaf_At_First_Slot_Greater_Or_Equal_To_Target()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50, 60, 70, 80);

        var steps = Execute(page, SeekBounds.Equality(TestKey.Of(50)), ScanDirection.Forward);

        var entry = steps.OfType<AccessStep.ProbeResult>().Single();

        Assert.Equal(4, entry.Slot);
    }

    [Fact]
    public void Seek_Descends_Node_Page_At_Last_Separator_Not_Greater_Than_Target()
    {
        var page = new TestIndexPage(new PageAddress(1, 100), [0, 171, 339, 507, 675, 843, 1011], level: 1);

        var steps = Execute(page, SeekBounds.Equality(TestKey.Of(1000)), ScanDirection.Forward);

        var entry = steps.OfType<AccessStep.ProbeResult>().Single();

        Assert.Equal(5, entry.Slot);

        var descend = steps.OfType<AccessStep.Descend>().Single();

        Assert.Equal(5, descend.Slot);
        Assert.Equal(new PageAddress(1, 843), descend.ChildPage);

        Assert.DoesNotContain(steps, s => s is AccessStep.Row);
    }

    [Fact]
    public void Seek_Descends_Node_Page_Left_Of_An_Equal_Separator()
    {
        var page = new TestIndexPage(new PageAddress(1, 100), [0, 171, 339], level: 1);

        var steps = Execute(page, SeekBounds.Equality(TestKey.Of(171)), ScanDirection.Forward);

        var entry = steps.OfType<AccessStep.ProbeResult>().Single();

        Assert.Equal(0, entry.Slot);
    }

    [Fact]
    public void Seek_Descends_Node_Page_At_First_Slot_When_Target_Is_Before_All_Separators()
    {
        var page = new TestIndexPage(new PageAddress(1, 100), [0, 171, 339], level: 1);

        var steps = Execute(page, SeekBounds.Equality(TestKey.Of(-10)), ScanDirection.Forward);

        var entry = steps.OfType<AccessStep.ProbeResult>().Single();

        Assert.Equal(0, entry.Slot);
    }

    [Fact]
    public void Exclusive_Seek_Descends_Node_Page_At_An_Equal_Separator()
    {
        var page = new TestIndexPage(new PageAddress(1, 100), [0, 171, 339], level: 1);

        var bounds = new SeekBounds
        {
            StartValue = TestKey.Of(171),
            IsStartInclusive = false,
            CompareWidth = 1
        };

        var steps = Execute(page, bounds, ScanDirection.Forward);

        var entry = steps.OfType<AccessStep.ProbeResult>().Single();

        Assert.Equal(1, entry.Slot);
    }

    [Fact]
    public void Backward_Seek_Starts_At_A_Row_Equal_To_An_Inclusive_End_Bound()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50);

        var bounds = new SeekBounds
        {
            EndValue = TestKey.Of(30),
            IsEndInclusive = true,
            CompareWidth = 1
        };

        var steps = Execute(page, bounds, ScanDirection.Backward);

        var row = steps.OfType<AccessStep.Row>().First();

        Assert.Equal(2, row.Slot);
    }

    [Fact]
    public void Probe_Result_Carries_The_Positioning_Rule()
    {
        var leaf = TestIndexPage.Create(10, 20, 30, 40, 50);

        var leafSteps = Execute(leaf, SeekBounds.Equality(TestKey.Of(30)), ScanDirection.Forward);

        Assert.Equal(SeekRule.LowestGreaterOrEqual, leafSteps.OfType<AccessStep.ProbeResult>().Single().Rule);

        var node = new TestIndexPage(new PageAddress(1, 100), [0, 171, 339], level: 1);

        var nodeSteps = Execute(node, SeekBounds.Equality(TestKey.Of(200)), ScanDirection.Forward);

        var nodeResult = nodeSteps.OfType<AccessStep.ProbeResult>().Single();

        Assert.Equal(SeekRule.HighestLess, nodeResult.Rule);
        Assert.Equal("< 200", PredicateWriter.ToText(PredicateWriter.Write(nodeResult)));
    }

    [Fact]
    public void Seek_States_Its_Goal_Before_Probing()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50);

        var steps = Execute(page, SeekBounds.Equality(TestKey.Of(30)), ScanDirection.Forward);

        var probeStart = steps.OfType<AccessStep.ProbeStart>().Single();

        Assert.Equal(SeekRule.LowestGreaterOrEqual, probeStart.Rule);
        Assert.Equal(5, probeStart.SlotCount);
        Assert.True(steps.IndexOf(probeStart) < steps.IndexOf(steps.OfType<AccessStep.Probe>().First()));
    }

    [Fact]
    public void Unbounded_Start_Emits_A_Probe_Start_Without_A_Rule()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50);

        var bounds = new SeekBounds
        {
            EndValue = TestKey.Of(500),
            IsEndInclusive = false,
            CompareWidth = 1
        };

        var steps = Execute(page, bounds, ScanDirection.Forward);

        var probeStart = steps.OfType<AccessStep.ProbeStart>().Single();

        Assert.Null(probeStart.Rule);
        Assert.Equal(ScanDirection.Forward, probeStart.Direction);
        Assert.Empty(steps.OfType<AccessStep.Probe>());
        Assert.Equal(0, steps.OfType<AccessStep.ProbeResult>().Single().Slot);
    }

    [Fact]
    public void Continuation_Page_Walks_From_The_First_Slot_Without_Probing()
    {
        var page = TestIndexPage.Create(60, 70, 80);

        var bounds = SeekBounds.Between(TestKey.Of(50), TestKey.Of(75));

        var steps = IndexSeekExecutor.Execute(page, bounds, ScanDirection.Forward, isContinuation: true).ToList();

        Assert.DoesNotContain(steps, s => s is AccessStep.ProbeStart or AccessStep.Probe or AccessStep.ProbeResult);

        Assert.Equal(0, steps.OfType<AccessStep.Row>().First().Slot);
        Assert.Equal(2, steps.OfType<AccessStep.RangeEnd>().Single().Slot);
    }

    [Fact]
    public void Rows_Without_A_Residual_Are_Not_Tagged_As_Filtered()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50);

        var steps = Execute(page, SeekBounds.Equality(TestKey.Of(30)), ScanDirection.Forward);

        Assert.All(steps.OfType<AccessStep.Row>(), r => Assert.False(r.HasResidual));
    }

    [Fact]
    public void Residual_Rows_Are_Tagged_As_Filtered()
    {
        var page = TestIndexPage.Create(10, 20, 30);

        var residual = new AccessPredicate.Comparison(
            new AccessExpression.Column(-1, "Id"),
            ComparisonOperator.GreaterThanOrEqual,
            new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, 20)));

        var steps = IndexSeekExecutor.Execute(page, SeekBounds.All, ScanDirection.Forward, residual).ToList();

        var rows = steps.OfType<AccessStep.Row>().ToList();

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.True(r.HasResidual));
        Assert.Equal(RowOutcome.NoMatch, rows[0].Outcome);
        Assert.Equal(RowOutcome.Match, rows[1].Outcome);
        Assert.Equal(RowOutcome.Match, rows[2].Outcome);

        Assert.Null(rows[0].EmittedRecord);
        Assert.NotNull(rows[1].EmittedRecord);
        Assert.NotNull(rows[2].EmittedRecord);
    }

    [Fact]
    public void Row_Goal_Stops_The_Walk_After_The_Match()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50);

        var steps = IndexSeekExecutor.Execute(page, SeekBounds.Equality(TestKey.Of(30)), ScanDirection.Forward, rowGoal: 1).ToList();

        Assert.Equal(StopReason.RowGoalMet, steps.OfType<AccessStep.Stopped>().Single().Reason);
        Assert.DoesNotContain(steps, s => s is AccessStep.RangeEnd);
        Assert.Single(steps.OfType<AccessStep.Row>());
    }

    [Fact]
    public void Range_End_Carries_The_Failing_Comparison()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50);

        var steps = Execute(page, SeekBounds.Equality(TestKey.Of(30)), ScanDirection.Forward);

        var rangeEnd = steps.OfType<AccessStep.RangeEnd>().Single();

        Assert.Equal(3, rangeEnd.Slot);
        Assert.Equal("40 > 30", PredicateWriter.ToText(PredicateWriter.Write(rangeEnd)));
    }

    [Fact]
    public void Probes_Narrow_The_Search_Window()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50, 60, 70, 80);

        var steps = Execute(page, SeekBounds.Equality(TestKey.Of(50)), ScanDirection.Forward);

        var probes = steps.OfType<AccessStep.Probe>().ToList();

        Assert.NotEmpty(probes);

        for (var index = 1; index < probes.Count; index++)
        {
            Assert.True(probes[index].High - probes[index].Low < probes[index - 1].High - probes[index - 1].Low);
        }

        Assert.Equal(probes.Count, probes[^1].Counters.Comparisons);
    }

    [Fact]
    public void Probes_Carry_The_Compared_Keys()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50, 60, 70, 80);

        var steps = Execute(page, SeekBounds.Equality(TestKey.Of(50)), ScanDirection.Forward);

        var probe = steps.OfType<AccessStep.Probe>().First();

        Assert.Equal(4, probe.Middle);
        Assert.Equal(1, probe.Width);
        Assert.Equal(8, probe.SlotCount);
        Assert.False(probe.SearchRight);
        Assert.Equal("50 = 50", PredicateWriter.ToText(PredicateWriter.Write(probe)));
    }

    [Fact]
    public void Backward_Seek_Starts_Before_An_Exclusive_End_Bound()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50);

        var bounds = new SeekBounds
        {
            EndValue = TestKey.Of(30),
            IsEndInclusive = false,
            CompareWidth = 1
        };

        var steps = Execute(page, bounds, ScanDirection.Backward);

        var row = steps.OfType<AccessStep.Row>().First();

        Assert.Equal(1, row.Slot);
    }

    private static List<AccessStep> Execute(TestIndexPage page, SeekBounds bounds, ScanDirection direction)
    {
        return [.. IndexSeekExecutor.Execute(page, bounds, direction)];
    }
}
