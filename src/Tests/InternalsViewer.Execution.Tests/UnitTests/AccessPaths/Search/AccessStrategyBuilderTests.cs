using System.Data;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Execution.Tests.UnitTests.AccessPaths.Search;

public class AccessStrategyBuilderTests
{
    [Fact]
    public void Unique_Equality_Explains_The_Row_Goal()
    {
        var strategy = AccessStrategyBuilder.Build(UniqueIndex(), SeekBounds.Equality(TestKey.Of([5000], "Id")), ScanDirection.Forward, 1);

        Assert.Equal(1, strategy.RowGoal);
        Assert.NotNull(strategy.RowGoalReason);
        Assert.Contains("unique", strategy.RowGoalReason);

        Assert.Equal(4, strategy.Phases.Length);
        Assert.Equal("Id < 5000", Text(strategy.Phases[0]));
        Assert.Equal("Id >= 5000", Text(strategy.Phases[1]));
        Assert.Equal("Id > 5000", Text(strategy.Phases[2]));
        Assert.Contains("row goal", strategy.Phases[3].Lead);
    }

    [Fact]
    public void Exclusive_Range_Uses_The_Exclusive_Operators()
    {
        var bounds = SeekBounds.Between(TestKey.Of([100], "Id"), TestKey.Of([500], "Id"), false, false);

        var strategy = AccessStrategyBuilder.Build(UniqueIndex(), bounds, ScanDirection.Forward, null);

        Assert.Null(strategy.RowGoalReason);

        Assert.Equal("Id <= 100", Text(strategy.Phases[0]));
        Assert.Equal("Id > 100", Text(strategy.Phases[1]));
        Assert.Equal("Id >= 500", Text(strategy.Phases[2]));
        Assert.Equal("Stop when a key leaves the range", strategy.Phases[3].Lead);

        Assert.Same(bounds, strategy.Bounds);
        Assert.Equal(ScanDirection.Forward, strategy.Direction);
    }

    [Fact]
    public void Unbounded_Start_Descends_By_First_Pointer()
    {
        var bounds = new SeekBounds
        {
            EndValue = TestKey.Of([500], "Id"),
            IsEndInclusive = false,
            CompareWidth = 1
        };

        var strategy = AccessStrategyBuilder.Build(UniqueIndex(), bounds, ScanDirection.Forward, null);

        Assert.True(strategy.Phases[0].Condition.IsDefaultOrEmpty);
        Assert.Contains("first down page pointer", strategy.Phases[0].Lead);
        Assert.Equal("Id >= 500", Text(strategy.Phases[2]));
    }

    [Fact]
    public void Unbounded_Scan_With_A_Residual_Describes_A_Full_Walk()
    {
        var residual = new AccessPredicate.Like(new AccessExpression.Column(-1, "TextField"), "%Test%");

        var strategy = AccessStrategyBuilder.Build(UniqueIndex(), SeekBounds.All, ScanDirection.Forward, null, residual);

        Assert.Contains("first down page pointer", strategy.Phases[0].Lead);
        Assert.Contains("first slot", strategy.Phases[1].Lead);
        Assert.Contains("end of the index", strategy.Phases[2].Lead);
        Assert.Equal("Stop at the end of the index", strategy.Phases[3].Lead);

        Assert.Same(residual, strategy.Residual);
    }

    private static string Text(AccessStrategyPhase phase)
    {
        return PredicateWriter.ToText(phase.Condition);
    }

    private static IndexStructure UniqueIndex()
    {
        return new IndexStructure(0)
        {
            IsUnique = true,
            Columns =
            [
                new IndexColumnStructure
                {
                    ColumnId = 1,
                    ColumnName = "Id",
                    DataType = SqlDbType.Int,
                    IsKey = true,
                    IsIndexKey = true
                }
            ]
        };
    }
}
