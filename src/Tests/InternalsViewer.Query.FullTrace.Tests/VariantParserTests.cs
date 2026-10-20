using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class VariantParserTests
{
    private const ulong Variant = 0x1F4_0000_1000;

    [Theory]
    [InlineData(0, 0, TraceEvaluationResult.False)]
    [InlineData(0, 1, TraceEvaluationResult.True)]
    [InlineData(3, 0, TraceEvaluationResult.Unknown)]
    [InlineData(3, 1, TraceEvaluationResult.Unknown)]
    public void The_Variant_Status_And_Value_Give_The_Predicate_Result(byte status, byte value, TraceEvaluationResult expected)
    {
        Assert.Equal(expected, VariantParser.GetPredicateResult(Replay(status, value), Variant));
    }

    [Fact]
    public void A_Value_That_Is_Not_A_Boolean_Has_No_Predicate_Result()
    {
        Assert.Null(VariantParser.GetPredicateResult(Replay(0, 5), Variant));
    }

    [Fact]
    public void An_Unreadable_Variant_Has_No_Predicate_Result()
    {
        Assert.Null(VariantParser.GetPredicateResult(new FakeTraceReplay(), Variant));
    }

    private static FakeTraceReplay Replay(byte status, byte value) => new FakeTraceReplay().Write(Variant, status).Write(Variant + 8, value);
}
