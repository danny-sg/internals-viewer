using InternalsViewer.Query.FullTrace.Evaluations;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class EvaluationHookTests
{
    private const ulong Function = 0x1000;

    private const ulong Result = 0x1F4_0000_1000;

    [Fact]
    public void The_Result_Variant_Is_Read_When_The_Evaluation_Returns()
    {
        var hook = new EvaluationHook(new EvaluationFunctions([Function]));

        var replay = new FakeTraceReplay();

        hook.OnEntry(new TraceHookCall { Function = Function, Id = 1, Call = 4, Arguments = new[] { 0ul, 0ul, Result, 0ul } }, replay);

        replay.Write(Result, (byte)0).Write(Result + 8, (byte)1);

        hook.OnReturn(new TraceHookCall { Function = Function, Id = 1, Call = 4 }, replay);

        Assert.Equal(new EvaluationCapture(Function, 4, TraceEvaluationResult.True), Assert.Single(hook.Build()));
    }

    [Fact]
    public void An_Evaluation_Without_A_Result_Variant_Is_Not_Captured()
    {
        var hook = new EvaluationHook(new EvaluationFunctions([Function]));

        var replay = new FakeTraceReplay().Write(0, (byte)0).Write(8, (byte)1);

        hook.OnEntry(new TraceHookCall { Function = Function, Id = 1, Call = 4, Arguments = new[] { 0ul, 0ul, 0ul, 0ul } }, replay);

        hook.OnReturn(new TraceHookCall { Function = Function, Id = 1, Call = 4 }, replay);

        Assert.Empty(hook.Build());
    }

    [Fact]
    public void An_Abandoned_Evaluation_Is_Not_Captured()
    {
        var hook = new EvaluationHook(new EvaluationFunctions([Function]));

        var replay = new FakeTraceReplay().Write(Result, (byte)0).Write(Result + 8, (byte)1);

        hook.OnEntry(new TraceHookCall { Function = Function, Id = 1, Call = 4, Arguments = new[] { 0ul, 0ul, Result, 0ul } }, replay);

        hook.OnAbandoned(new TraceHookCall { Function = Function, Id = 1, Call = 4 });

        hook.OnReturn(new TraceHookCall { Function = Function, Id = 1, Call = 4 }, replay);

        Assert.Empty(hook.Build());
    }
}
