using System.Data;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Tests.UnitTests.DataAccess.AccessPaths;

public class SeekStrategyBuilderTests
{
    [Fact]
    public void Unique_Equality_Explains_The_Row_Goal()
    {
        var strategy = SeekStrategyBuilder.Build(UniqueIndex(), SeekBounds.Equality(TestKey.Of([5000], "Id")), ScanDirection.Forward, 1);

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

        var strategy = SeekStrategyBuilder.Build(UniqueIndex(), bounds, ScanDirection.Forward, null);

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

        var strategy = SeekStrategyBuilder.Build(UniqueIndex(), bounds, ScanDirection.Forward, null);

        Assert.True(strategy.Phases[0].Condition.IsDefaultOrEmpty);
        Assert.Contains("first down page pointer", strategy.Phases[0].Lead);
        Assert.Equal("Id >= 500", Text(strategy.Phases[2]));
    }

    private static string Text(SeekStrategyPhase phase)
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
