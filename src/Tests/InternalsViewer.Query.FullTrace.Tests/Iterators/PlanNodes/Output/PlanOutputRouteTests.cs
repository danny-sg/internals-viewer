using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators.PlanNodes.Output;

[Trait("Category", "Unit")]
public class PlanOutputRouteTests
{
    private const ulong PlanObject = 0x1F4_0000_1000;

    private const ulong Outputs = 0x1F4_0000_1100;

    private const ulong OutputArray = 0x1F4_0000_1200;

    private const ulong Reference = 0x1F4_0000_1300;

    private const ulong OtherReference = 0x1F4_0000_1340;

    private const ulong Producer = 0x1F4_0000_1400;

    private const ulong Iterator = 0x1F4_0000_2000;

    private const ulong ProducerIterator = 0x1F4_0000_2400;

    private const ulong QueryContext = 0x1F4_0000_3400;

    private const ulong Context = 0x1F4_0000_3000;

    private const ulong Values = 0x1F4_0000_4000;

    private const ulong Value = 0x1F4_0000_4100;

    private const ulong RowBuffer = 0x1F4_0000_6000;

    private const ulong Bindings = 0x1F4_0000_7100;

    private const ulong InputValues = 0x1F4_0000_8100;

    private const ulong InputValue = 0x1F4_0000_8200;

    private const ulong Rowset = 0x1F4_0000_9000;

    private const ulong RowContext = 0x1F4_0000_A000;

    private const ulong StatementValues = 0x1F4_0000_B000;

    private const ulong StatementValue = 0x1F4_0000_B100;

    [Fact]
    public void A_Row_Buffer_Belongs_To_The_Operator_Whose_Iterator_Holds_It()
    {
        var routes = Create(false, (Operator(0, PlanObject, Iterator), []), (Operator(1, Producer, ProducerIterator), Scan()));

        Assert.Empty(routes[0].FromIterator);

        Assert.Equal("RowBuffer(BufferEntry#0,BufferEntry#1)", Describe(routes[1].FromIterator));
    }

    [Fact]
    public void A_Join_Input_Value_Reads_The_Entry_Of_The_Operator_Below()
    {
        PlanOutputLink[] join =
        [
            Link(PlanOutputLinkType.InputValues, InputValues, Iterator, 0xC8),
            Link(PlanOutputLinkType.InputValue, InputValue, InputValues, 0x00),
            Link(PlanOutputLinkType.BufferEntry, RowBuffer + 0x20, InputValue, 0x08, EngineLayout.Latest.ExecValue.EntryField)
        ];

        var routes = Create(false, (Operator(0, PlanObject, Iterator), join), (Operator(1, Producer, ProducerIterator), Scan()));

        Assert.Equal("InputValues(InputValue#0)", Describe(routes[0].FromIterator));

        var input = routes[0].FromIterator[0].Children[0];

        Assert.Equal([(1, routes[1].FromIterator[0].Children[1])], input.Targets.Select(t => (t.NodeId, t.Node)));
    }

    [Fact]
    public void A_Row_Slot_Reads_The_Rowset_The_Scan_Holds_In_The_Same_Block()
    {
        PlanOutputLink[] join =
        [
            Link(PlanOutputLinkType.OutputValues, Values, Iterator, 0x58),
            Link(PlanOutputLinkType.Value, Value, Values, 0x00),
            Link(PlanOutputLinkType.RowContext, RowContext, Value, 0x2C),
            Link(PlanOutputLinkType.RowSlot, RowContext + 0x08, RowContext, 0x08),
            Link(PlanOutputLinkType.Rowset, Rowset + 0xB8, RowContext + 0x08, 0x00)
        ];

        PlanOutputLink[] scan = [Link(PlanOutputLinkType.Rowset, Rowset + 0x70, ProducerIterator, 0x48)];

        var routes = Create(false, (Operator(0, PlanObject, Iterator), join), (Operator(1, Producer, ProducerIterator), scan));

        var slot = routes[0].FromIterator[0].Children[0].Children[0].Children[0];

        Assert.Equal([(1, routes[1].FromIterator[0])], slot.Targets.Select(t => (t.NodeId, t.Node)));
    }

    [Fact]
    public void An_Entry_And_Its_Binding_Are_Named_By_The_Operators_Own_Output_Column()
    {
        PlanOutputLink[] storage =
        [
            .. Scan(),
            Link(PlanOutputLinkType.ColumnBindings, Bindings, ProducerIterator, 0x88),
            Link(PlanOutputLinkType.ColumnBinding, Bindings + 0x50, Bindings, 0x50, EngineLayout.Latest.RowBindings.ColumnBindingField)
                with { Slot = 1 }
        ];

        PlanOutputLink[] links = [Link(PlanOutputLinkType.BufferEntry, RowBuffer + 0x20, RowBuffer, 0x20)];

        var routes = Create(false, (Operator(1, Producer, ProducerIterator, Path(links, Stage(Producer))), storage));

        Assert.Equal(0, routes[1].FromIterator[0].Children[1].Column);

        Assert.Equal(0, routes[1].FromIterator[1].Children[0].Column);

        Assert.Equal([(1, routes[1].FromIterator[0].Children[1])], routes[1].Columns.Select(c => (c.NodeId, c.Node)));
    }

    [Fact]
    public void A_Passed_Through_Column_Is_Held_By_The_Operator_Below()
    {
        var path = Path([.. Column(Reference, 0x00), .. Producing()], Stage(Producer));

        var routes = Create(false, (Operator(0, PlanObject, Iterator, path), []), (Operator(1, Producer, ProducerIterator), Scan()));

        Assert.Equal([(1, routes[1].FromIterator[0].Children[1])], routes[0].Columns.Select(c => (c.NodeId, c.Node)));

        Assert.Equal(-1, routes[1].FromIterator[0].Children[1].Column);
    }

    [Fact]
    public void With_The_Plan_Object_A_Passed_Through_Column_Is_Read_By_Its_Reference()
    {
        var path = Path([.. Column(Reference, 0x00), .. Producing()], Stage(Producer));

        var routes = Create(true, (Operator(0, PlanObject, Iterator, path), []), (Operator(1, Producer, ProducerIterator), Scan()));

        Assert.Equal("OutputList(OutputArray(Reference#0(Producer(Iterator))))", Describe(routes[0].FromPlanObject));

        var reference = routes[0].FromPlanObject[0].Children[0].Children[0];

        Assert.Equal([(1, routes[1].FromIterator[0].Children[1])], reference.Targets.Select(t => (t.NodeId, t.Node)));
    }

    [Fact]
    public void Compiled_Columns_Share_Their_Output_List_And_Branch_At_Their_References()
    {
        var first = Path([.. Column(Reference, 0x00), .. Producing()], Stage(PlanObject));

        var second = Path([.. Column(OtherReference, 0x08), .. Producing(OtherReference)], Stage(PlanObject));

        var routes = Create(true, (Operator(0, PlanObject, Iterator, first, second), []));

        Assert.Equal("OutputList(OutputArray(Reference#0(Producer(Iterator)),Reference#1(Producer(Iterator))))",
                     Describe(routes[0].FromPlanObject));

        Assert.Equal([0, 1], routes[0].FromPlanObject[0].Children[0].Children.Select(n => n.Column));
    }

    [Fact]
    public void The_Shared_Execution_Context_Is_Left_Out()
    {
        PlanOutputLink[] storage =
        [
            Link(PlanOutputLinkType.ExecutionContext, Context, ProducerIterator, 0x18, isRoute: true),
            Link(PlanOutputLinkType.QueryContext, QueryContext, Context, 0x1B8, isRoute: true),
            .. Scan()
        ];

        var routes = Create(false, (Operator(1, Producer, ProducerIterator), storage));

        Assert.Equal("RowBuffer(BufferEntry#0,BufferEntry#1)", Describe(routes[1].FromIterator));
    }

    private static Dictionary<int, PlanOutputRoute> Create(bool isPlanObjectIncluded,
                                                           params (PlanOutputRouteOperator Operator, PlanOutputLink[] Storage)[] operators)
        => PlanOutputRoute.Create([.. operators.Select(o => o.Operator)],
                                  operators.ToDictionary(o => o.Operator.NodeId, o => (IReadOnlyList<PlanOutputLink>)o.Storage),
                                  l => l.Address & ~0xFFFUL,
                                  isPlanObjectIncluded);

    private static PlanOutputRouteOperator Operator(int nodeId, ulong planObject, ulong iterator, params PlanOutputPath[] paths)
        => new(nodeId, planObject, iterator, paths);

    private static PlanOutputPath Path(PlanOutputLink[] links, params PlanOutputStage[] stages) => new() { Links = links, Stages = stages };

    private static PlanOutputStage Stage(ulong planObject)
        => new(Reference, 0, -1)
        {
            PlanObject = planObject,
            Value = new PlanOutputValueSource(PlanOutputLinkType.BufferEntry, RowBuffer + 0x20, [], null)
        };

    private static PlanOutputLink[] Scan() =>
    [
        Link(PlanOutputLinkType.RowBuffer, RowBuffer, ProducerIterator, 0x80, EngineLayout.Latest.RowsetIterator.RowBufferField),
        Link(PlanOutputLinkType.BufferEntry, RowBuffer, RowBuffer, 0x00, EngineLayout.Latest.RowBuffer.EntryField),
        Link(PlanOutputLinkType.BufferEntry, RowBuffer + 0x20, RowBuffer, 0x20, EngineLayout.Latest.RowBuffer.EntryField)
    ];

    private static PlanOutputLink[] Column(ulong reference, int offset) =>
    [
        Link(PlanOutputLinkType.OutputList, Outputs, PlanObject, 0x30, EngineLayout.Latest.PlanObject.OutputColumnsField, isRoute: true),
        Link(PlanOutputLinkType.OutputArray, OutputArray, Outputs, 0x10, EngineLayout.Latest.OutputList.ColumnsField, isRoute: true),
        Link(PlanOutputLinkType.Reference, reference, OutputArray, offset, EngineLayout.Latest.OutputList.ColumnField, isRoute: true)
    ];

    private static PlanOutputLink[] Producing(ulong reference = Reference) =>
    [
        Link(PlanOutputLinkType.Producer, Producer, reference, 0x50, isRoute: true),
        Link(PlanOutputLinkType.Iterator, ProducerIterator, Producer, 0x00, isRoute: true),
        Link(PlanOutputLinkType.RowBuffer, RowBuffer, ProducerIterator, 0x80),
        Link(PlanOutputLinkType.BufferEntry, RowBuffer + 0x20, RowBuffer, 0x20, EngineLayout.Latest.RowBuffer.EntryField)
    ];

    private static PlanOutputLink Link(PlanOutputLinkType type,
                                       ulong address,
                                       ulong source,
                                       int sourceOffset,
                                       MemoryField? field = null,
                                       bool isRoute = false)
        => new(type, address, At(1))
        {
            Source = source,
            SourceOffset = sourceOffset,
            Field = field,
            IsRoute = isRoute
        };

    private static string Describe(IEnumerable<PlanOutputRouteNode> nodes)
        => string.Join(",", nodes.Select(n => $"{n.Link.Type}{(n.Index >= 0 ? $"#{n.Index}" : string.Empty)}"
                                              + (n.Children.Count > 0 ? $"({Describe(n.Children)})" : string.Empty)));
}
