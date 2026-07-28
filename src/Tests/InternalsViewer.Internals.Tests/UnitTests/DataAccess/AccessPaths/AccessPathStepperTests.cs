using InternalsViewer.Internals.DataAccess.AccessPaths;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.Executors;

namespace InternalsViewer.Internals.Tests.UnitTests.DataAccess.AccessPaths;

public class AccessPathStepperTests
{
    [Fact]
    public void Stepper_Does_Not_Advance_Until_Moved()
    {
        var stepper = new AccessPathStepper(Scan(TestIndexPage.Create(1, 2, 3)));

        Assert.Null(stepper.Current);
        Assert.Empty(stepper.History);
        Assert.False(stepper.IsComplete);
    }

    [Fact]
    public void First_Step_Is_Entering_The_Page()
    {
        var page = TestIndexPage.Create(1, 2, 3);

        var stepper = new AccessPathStepper(Scan(page));

        Assert.True(stepper.MoveNext());

        var step = Assert.IsType<AccessStep.ReadPage>(stepper.Current);

        Assert.Equal(page.PageAddress, step.PageAddress);
        Assert.Equal(3, step.SlotCount);
        Assert.Equal(1, step.Counters.PagesRead);
    }

    [Fact]
    public void History_Accumulates_In_Order()
    {
        var stepper = new AccessPathStepper(Scan(TestIndexPage.Create(1, 2)));

        stepper.RunToEnd();

        Assert.Collection(stepper.History,
                          step => Assert.IsType<AccessStep.ReadPage>(step),
                          step => Assert.IsType<AccessStep.ProbeResult>(step),
                          step => Assert.Equal(0, Assert.IsType<AccessStep.Row>(step).Slot),
                          step => Assert.Equal(1, Assert.IsType<AccessStep.Row>(step).Slot),
                          step => Assert.IsType<AccessStep.Stopped>(step));
    }

    [Fact]
    public void Move_Next_Returns_False_Once_Complete()
    {
        var stepper = new AccessPathStepper(Scan(TestIndexPage.Create(1)));

        stepper.RunToEnd();

        Assert.True(stepper.IsComplete);
        Assert.False(stepper.MoveNext());
    }

    [Fact]
    public void Counters_Track_The_Current_Step()
    {
        var stepper = new AccessPathStepper(Scan(TestIndexPage.Create(1, 2, 3)));

        stepper.RunTo<AccessStep.Row>();

        Assert.Equal(1, stepper.Counters.RowsRead);

        stepper.MoveNext();

        Assert.Equal(2, stepper.Counters.RowsRead);
    }

    [Fact]
    public void History_Steps_Keep_The_Totals_They_Were_Produced_With()
    {
        var stepper = new AccessPathStepper(Scan(TestIndexPage.Create(1, 2, 3)));

        stepper.RunToEnd();

        var rows = stepper.History.OfType<AccessStep.Row>().ToList();

        Assert.Equal(1, rows[0].Counters.RowsRead);
        Assert.Equal(2, rows[1].Counters.RowsRead);
        Assert.Equal(3, rows[2].Counters.RowsRead);
    }

    [Fact]
    public void Run_To_Stops_On_The_Requested_Step()
    {
        var stepper = new AccessPathStepper(Scan(TestIndexPage.Create(1, 2, 3)));

        var stopped = stepper.RunTo<AccessStep.Stopped>();

        Assert.NotNull(stopped);
        Assert.Equal(StopReason.PageExhausted, stopped.Reason);
    }

    [Fact]
    public void Run_To_Returns_Null_When_The_Step_Never_Occurs()
    {
        var stepper = new AccessPathStepper(Scan(TestIndexPage.Create(1, 2)));

        Assert.Null(stepper.RunTo<AccessStep.Descend>());
        Assert.True(stepper.IsComplete);
    }

    [Fact]
    public void Restart_Replays_From_The_Beginning()
    {
        var stepper = new AccessPathStepper(Scan(TestIndexPage.Create(1, 2, 3)));

        var first = stepper.RunToEnd();

        stepper.Restart();

        Assert.Null(stepper.Current);
        Assert.Empty(stepper.History);
        Assert.False(stepper.IsComplete);

        Assert.Equal(first, stepper.RunToEnd());
    }

    [Fact]
    public void Seek_Probes_Can_Be_Stepped_Through()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50, 60, 70, 80);

        var bounds = SeekBounds.Equality(TestKey.Of(50));

        var executor = new PageSeekExecutor(new TestRowBinder());

        var stepper = new AccessPathStepper(executor.Execute(page, bounds, ScanDirection.Forward));

        stepper.RunToEnd();

        var probes = stepper.History.OfType<AccessStep.Probe>().ToList();

        Assert.NotEmpty(probes);

        for (var index = 1; index < probes.Count; index++)
        {
            var window = probes[index].High - probes[index].Low;
            var previous = probes[index - 1].High - probes[index - 1].Low;

            Assert.True(window < previous, "Each probe should narrow the search window");
        }

        Assert.Equal(probes.Count, probes[^1].Counters.Comparisons);
    }

    private static IEnumerable<AccessStep> Scan(TestIndexPage page)
    {
        return new PageScanExecutor(new TestRowBinder()).Execute(page);
    }
}
