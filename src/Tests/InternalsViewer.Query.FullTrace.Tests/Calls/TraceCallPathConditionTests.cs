using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Tests.Calls;

[Trait("Category", "Unit")]
public class TraceCallPathConditionTests
{
    private static readonly ArgumentSlot Count = new("count", "int", ArgumentLocation.Register, 1, "RDX");

    private static readonly ExecutionOperatorEvent Filter = new()
    {
        Name = "Filter",
        OperatorDescription = "Filter",
        PlanNodeIdentifier = new PlanNodeIdentifier(1, 2)
    };

    [Theory]
    [InlineData(TraceConditionTarget.CalledBy, "CQScanTableScanNew::GetRow", true, false)]
    [InlineData(TraceConditionTarget.CalledBy, "*Filter*::GetRow", true, true)]
    [InlineData(TraceConditionTarget.CalledBy, "sqlmin!CQScanFilterNew::GetRow", true, true)]
    [InlineData(TraceConditionTarget.CalledBy, "sqltses!CQScanFilterNew::GetRow", false, false)]
    [InlineData(TraceConditionTarget.CalledDirectlyBy, "CQScanTableScanNew::GetRow", false, false)]
    [InlineData(TraceConditionTarget.CalledDirectlyBy, "HeapDataSetSession::GetNextRowValuesInternal", true, false)]
    [InlineData(TraceConditionTarget.CalledDirectlyBy, "CQScanFilterNew::GetRow", false, true)]
    public void A_Call_Path_Condition_Matches_The_Callers_Of_The_Call(TraceConditionTarget target,
                                                                      string pattern,
                                                                      bool isScanMatch,
                                                                      bool isFilterMatch)
    {
        var (scanCompare, filterCompare) = BuildTree();

        var condition = Pattern(target, TraceConditionOperator.Equal, pattern);

        Assert.Equal((isScanMatch, isFilterMatch), (condition.MatchesNode(scanCompare), condition.MatchesNode(filterCompare)));
    }

    [Fact]
    public void Not_Equal_Matches_Calls_Without_That_Caller()
    {
        var (scanCompare, filterCompare) = BuildTree();

        var condition = Pattern(TraceConditionTarget.CalledBy, TraceConditionOperator.NotEqual, "CQScanTableScanNew");

        Assert.Equal((false, true), (condition.MatchesNode(scanCompare), condition.MatchesNode(filterCompare)));
    }

    [Theory]
    [InlineData("Filter", TraceConditionOperator.Equal, true)]
    [InlineData("Filter (Node 2)", TraceConditionOperator.Equal, true)]
    [InlineData("Table Scan", TraceConditionOperator.Equal, false)]
    [InlineData("Table Scan", TraceConditionOperator.NotEqual, true)]
    public void An_Operator_Condition_Matches_The_Operator_Name_Or_Its_Node(string value, TraceConditionOperator comparison, bool expected)
        => Assert.Equal(expected,
                        Pattern(TraceConditionTarget.Operator, comparison, value).MatchesOperator(Filter));

    [Fact]
    public void Conditions_That_Do_Not_Apply_Always_Match()
    {
        var (scanCompare, _) = BuildTree();

        var operatorCondition = Pattern(TraceConditionTarget.Operator, TraceConditionOperator.Equal, "Sort");

        var argumentCondition = new TraceCallCondition(TraceConditionTarget.Argument,
                                                       Count,
                                                       "int",
                                                       TraceConditionOperator.Equal,
                                                       "1");

        Assert.Equal((true, true), (operatorCondition.MatchesNode(scanCompare), argumentCondition.MatchesOperator(Filter)));
    }

    [Fact]
    public async Task A_Definition_Round_Trips_Through_Its_File()
    {
        var definition = new TraceTickDefinition("Scan Compares",
                                                 "SqlTsEs",
                                                 "CEsExec::FastCompare1",
                                                 "#5CD66B",
                                                 1.5,
                                                 2,
                                                 [
                                                     new TraceCallCondition(TraceConditionTarget.Argument,
                                                                            Count,
                                                                            "int",
                                                                            TraceConditionOperator.GreaterThan,
                                                                            "0x10"),
                                                     Pattern(TraceConditionTarget.CalledBy,
                                                             TraceConditionOperator.Equal,
                                                             "CQScanTableScanNew::GetRow")
                                                 ]);

        using var stream = new MemoryStream();

        await TraceTickDefinition.WriteAsync(stream, [definition], CancellationToken.None);

        stream.Position = 0;

        var read = Assert.Single(await TraceTickDefinition.ReadAsync(stream, CancellationToken.None));

        Assert.Equal(definition with { Conditions = [] }, read with { Conditions = [] });
        Assert.Equal(definition.Conditions, read.Conditions);
    }

    private static TraceCallCondition Pattern(TraceConditionTarget target, TraceConditionOperator comparison, string value)
        => new(target, null, string.Empty, comparison, value);

    private static (CallStackNode ScanCompare, CallStackNode FilterCompare) BuildTree()
    {
        var tree = new CallStackTree();

        var filter = tree.AddCall(tree.Root, Frame("sqlmin", "CQScanFilterNew", "GetRow", 0x10), 1);

        var filterCompare = tree.AddCall(filter, Frame("SqlTsEs", "CEsExec", "FastCompare1", 0x20), 1);

        var scan = tree.AddCall(filter, Frame("sqlmin", "CQScanTableScanNew", "GetRow", 0x30), 1);

        var session = tree.AddCall(scan, Frame("sqlmin", "HeapDataSetSession", "GetNextRowValuesInternal", 0x40), 1);

        var scanCompare = tree.AddCall(session, Frame("SqlTsEs", "CEsExec", "FastCompare1", 0x20), 1);

        return (scanCompare, filterCompare);
    }

    private static CallstackFrame Frame(string module, string className, string method, uint rva) => new()
    {
        Module = module,
        Rva = rva,
        Address = rva,
        Resolved = new ResolvedCallstackFrame
        {
            ClassName = className,
            MethodName = method,
            SymbolCategory = SymbolCategory.Unknown
        }
    };
}
