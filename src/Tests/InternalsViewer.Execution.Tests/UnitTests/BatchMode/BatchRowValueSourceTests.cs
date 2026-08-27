using System.Collections.Immutable;
using System.Data;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.BatchMode.Vectors;

namespace InternalsViewer.Execution.Tests.UnitTests.BatchMode;

public class BatchRowValueSourceTests
{
    [Fact]
    public void A_Predicate_Over_Two_Of_Three_Columns_Reads_Both()
    {
        var batch = Create();

        var predicate = new AccessPredicate.And([Equals("Id", 2), Equals("Spread", 20)]);

        Assert.Equal([false, true, false], Rows(batch, predicate));
    }

    [Fact]
    public void Each_Column_In_The_Predicate_Resolves_Independently()
    {
        var batch = Create();

        Assert.Equal([true, false, false], Rows(batch, Equals("Id", 1)));

        Assert.Equal([false, false, true], Rows(batch, Equals("Spread", 30)));
    }

    [Fact]
    public void A_Predicate_Over_Two_Columns_That_Never_Agree_Matches_Nothing()
    {
        var batch = Create();

        var predicate = new AccessPredicate.And([Equals("Id", 1), Equals("Spread", 20)]);

        Assert.Equal([false, false, false], Rows(batch, predicate));
    }

    [Fact]
    public void A_Column_The_Predicate_Does_Not_Name_Is_Never_Decoded()
    {
        var batch = Create();

        Assert.Equal([false, true, false], Rows(batch, Equals("Id", 2)));

        Assert.Throws<ArgumentOutOfRangeException>(() => Rows(batch, Equals("Filler", 0)));
    }

    [Fact]
    public void A_Column_The_Batch_Does_Not_Hold_Reads_As_Null()
    {
        var batch = Create();

        var source = new BatchRowValueSource();

        source.Bind(batch);

        source.MoveTo(0);

        Assert.True(source.GetValue(0, "Missing").IsNull);

        Assert.True(source.GetValue(0).IsNull);
    }

    [Fact]
    public void An_Ordinal_Is_Ignored_So_A_Narrower_Read_Cannot_Land_On_The_Wrong_Column()
    {
        var batch = Create();

        var source = new BatchRowValueSource();

        source.Bind(batch);

        source.MoveTo(2);

        Assert.Equal(30, source.GetValue(0, "Spread").Numeric);

        Assert.Equal(3, source.GetValue(99, "Id").Numeric);
    }

    private static bool[] Rows(ExecutionBatch batch, AccessPredicate predicate)
    {
        var source = new BatchRowValueSource();

        source.Bind(batch);

        var results = new bool[batch.RowCount];

        for (var row = 0; row < batch.RowCount; row++)
        {
            source.MoveTo(row);

            results[row] = PredicateEvaluator.Evaluate(predicate, source, EvaluationContext.Now) == true;
        }

        return results;
    }

    private static AccessPredicate.Comparison Equals(string column, long value)
        => new(new AccessExpression.Column(-1, column),
               ComparisonOperator.Equal,
               new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.BigInt, value)));

    private static ExecutionBatch Create()
    {
        var batch = new ExecutionBatch(3, [Vector("Id"), Vector("Spread"), Vector("Filler")], new BatchDeepDataStore());

        for (var row = 0; row < 3; row++)
        {
            batch.Vectors[0].Slots[row] = Slot(row + 1);

            batch.Vectors[1].Slots[row] = Slot((row + 1) * 10);

            batch.Vectors[2].Slots[row] = new BatchSlot(0xFF);
        }

        batch.SetRowCount(3);

        return batch;
    }

    private static BatchVector Vector(string name)
        => new(new BatchColumn { Name = name, DataType = SqlDbType.BigInt }, 3);

    private static BatchSlot Slot(long value)
    {
        Assert.True(BatchSlotNormalizer.TryNormalize(value, out var slot));

        return slot;
    }
}
