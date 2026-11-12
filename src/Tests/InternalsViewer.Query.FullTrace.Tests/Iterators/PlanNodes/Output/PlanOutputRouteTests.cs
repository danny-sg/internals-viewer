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

    private const ulong InputReference = 0x1F4_0000_1500;

    private const ulong InputProducer = 0x1F4_0000_1600;

    private const ulong Iterator = 0x1F4_0000_2000;

    private const ulong ProducerIterator = 0x1F4_0000_2400;

    private const ulong InputIterator = 0x1F4_0000_2800;

    private const ulong Profiling = 0x1F4_0000_2E00;

    private const ulong Context = 0x1F4_0000_3000;

    private const ulong Map = 0x1F4_0000_3800;

    private const ulong Entry = 0x1F4_0000_3900;

    private const ulong InputEntry = 0x1F4_0000_3A00;

    private const ulong Values = 0x1F4_0000_4000;

    private const ulong Value = 0x1F4_0000_4100;

    private const ulong RowBuffer = 0x1F4_0000_6000;

    private const ulong InputRowBuffer = 0x1F4_0000_6400;

    private const ulong InputArray = 0x1F4_0000_8000;

    [Fact]
    public void Output_Columns_Share_Their_Output_List_And_Branch_At_Their_Entries()
    {
        PlanOutputLink[] first = [.. Column(Reference, 0x00), .. Producing(Producer, Entry, ProducerIterator, RowBuffer, 0x40)];

        PlanOutputLink[] second =
        [
            .. Column(OtherReference, 0x08),
            .. Producing(Producer, Entry, ProducerIterator, RowBuffer, 0x20, OtherReference)
        ];

        var route = Create(first, second);

        Assert.Equal("OutputList(OutputArray(Reference#0(Producer(MapEntry(Iterator(RowBuffer(BufferEntry#2))))),"
                     + "Reference#1(Producer(MapEntry(Iterator(RowBuffer(BufferEntry#1)))))))",
                     Describe(route.FromPlanObject));

        Assert.Equal([0, 1], route.FromPlanObject[0].Children[0].Children.Select(n => n.Column));

        Assert.Empty(route.FromIterator);
    }

    [Fact]
    public void An_Operators_Inputs_Hang_From_Its_Iterator()
    {
        PlanOutputLink[] links =
        [
            .. Column(Reference, 0x00),
            Link(PlanOutputLinkType.Producer, Producer, Reference, 0x50),
            Link(PlanOutputLinkType.MapEntry, Entry, Map, 0x08),
            Link(PlanOutputLinkType.Iterator, ProducerIterator, Entry, 0x10),
            Link(PlanOutputLinkType.Inputs, InputArray, Iterator, 0x08, EngineLayout.Iterator.InputsField),
            Link(PlanOutputLinkType.ProfilingIterator, Profiling, InputArray, 0x00, EngineLayout.Iterator.InputField),
            Link(PlanOutputLinkType.Iterator, ProducerIterator, Profiling, 0xA0),
            Link(PlanOutputLinkType.RowBuffer, RowBuffer, ProducerIterator, 0x80),
            Link(PlanOutputLinkType.BufferEntry, RowBuffer, RowBuffer, 0x00, EngineLayout.RowBuffer.EntryField)
        ];

        var route = Create(links);

        Assert.Equal("Inputs(ProfilingIterator#0(Iterator(RowBuffer(BufferEntry#0))))", Describe(route.FromIterator));

        Assert.Equal("OutputList(OutputArray(Reference#0(Producer(MapEntry(Iterator)))))", Describe(route.FromPlanObject));
    }

    [Fact]
    public void A_Column_Read_From_The_Operator_Below_Continues_From_The_Value_That_Reads_It()
    {
        PlanOutputLink[] links =
        [
            .. Column(Reference, 0x00),
            Link(PlanOutputLinkType.Producer, Producer, Reference, 0x50, isRoute: true),
            Link(PlanOutputLinkType.MapEntry, Entry, Map, 0x08, isRoute: true),
            Link(PlanOutputLinkType.Iterator, ProducerIterator, Entry, 0x10, isRoute: true),
            Link(PlanOutputLinkType.OutputValues, Values, ProducerIterator, 0x60),
            Link(PlanOutputLinkType.Value, Value, Values, 0x08),
            Link(PlanOutputLinkType.Input, InputReference, Value, 0x18, isRoute: true),
            .. Producing(InputProducer, InputEntry, InputIterator, InputRowBuffer, 0x20, InputReference, 1)
        ];

        var route = Create(links);

        Assert.Equal("Value#1(Input(Producer(MapEntry(Iterator(RowBuffer(BufferEntry#1))))))",
                     Describe(route.FromPlanObject[0].Children[0].Children[0].Children[0].Children[0].Children[0].Children[0].Children));
    }

    [Fact]
    public void A_Map_Entry_Shared_By_Two_Lookups_Leads_To_The_Iterator_Of_The_Latest()
    {
        PlanOutputLink[] links =
        [
            .. Column(Reference, 0x00),
            .. Producing(Producer, Entry, ProducerIterator, RowBuffer, 0x00),
            Link(PlanOutputLinkType.Input, InputReference, RowBuffer, 0x00, isRoute: true),
            .. Producing(InputProducer, Entry, InputIterator, InputRowBuffer, 0x20, InputReference, 1)
        ];

        var route = Create(links);

        var input = route.FromPlanObject[0].Children[0].Children[0].Children[0].Children[0].Children[0].Children[0].Children[0];

        Assert.Equal("Input(Producer(MapEntry(Iterator(RowBuffer(BufferEntry#1)))))", Describe([input.Children[0]]));
    }

    [Fact]
    public void Without_The_Plan_Object_A_Column_Starts_At_The_Iterator_Field_That_Holds_It()
    {
        PlanOutputLink[] links =
        [
            .. Column(Reference, 0x00),
            Link(PlanOutputLinkType.Producer, Producer, Reference, 0x50, isRoute: true),
            Link(PlanOutputLinkType.MapEntry, Entry, Map, 0x08, isRoute: true),
            Link(PlanOutputLinkType.Iterator, Iterator, Entry, 0x10, isRoute: true),
            Link(PlanOutputLinkType.OutputValues, Values, Iterator, 0x60),
            Link(PlanOutputLinkType.Value, Value, Values, 0x08),
            Link(PlanOutputLinkType.Input, InputReference, Value, 0x18, isRoute: true),
            .. Producing(InputProducer, InputEntry, InputIterator, InputRowBuffer, 0x20, InputReference, 1)
        ];

        var route = CreateFromIterator(links);

        Assert.Equal("OutputValues(Value#1)", Describe(route.FromIterator));

        Assert.Equal(0, route.FromIterator[0].Children[0].Column);

        Assert.Empty(route.FromPlanObject);
    }

    [Fact]
    public void Without_The_Plan_Object_A_Passed_Through_Column_Is_Reached_Through_The_Inputs()
    {
        PlanOutputLink[] links =
        [
            .. Column(Reference, 0x00),
            .. Producing(Producer, Entry, ProducerIterator, RowBuffer, 0x20),
            Link(PlanOutputLinkType.Inputs, InputArray, Iterator, 0x08, EngineLayout.Iterator.InputsField),
            Link(PlanOutputLinkType.ProfilingIterator, Profiling, InputArray, 0x00, EngineLayout.Iterator.InputField),
            Link(PlanOutputLinkType.Iterator, ProducerIterator, Profiling, 0xA0),
            Link(PlanOutputLinkType.RowBuffer, RowBuffer, ProducerIterator, 0x80),
            Link(PlanOutputLinkType.BufferEntry, RowBuffer + 0x20, RowBuffer, 0x20, EngineLayout.RowBuffer.EntryField)
        ];

        var route = CreateFromIterator(links);

        Assert.Equal("Inputs(ProfilingIterator#0(Iterator(RowBuffer(BufferEntry#1))))", Describe(route.FromIterator));

        Assert.Equal(0, route.FromIterator[0].Children[0].Children[0].Children[0].Children[0].Column);

        Assert.Empty(route.FromPlanObject);
    }

    [Fact]
    public void The_Shared_Execution_Context_Is_Left_Out()
    {
        var route = Create([.. Column(Reference, 0x00),
                            Link(PlanOutputLinkType.ExecutionContext, Context, Iterator, 0x18, isRoute: true),
                            Link(PlanOutputLinkType.IteratorMap, Map, Context, 0x290, isRoute: true),
                            .. Producing(Producer, Entry, ProducerIterator, RowBuffer, 0x00)]);

        Assert.DoesNotContain("Context", Describe(route.FromPlanObject));

        Assert.DoesNotContain("IteratorMap", Describe(route.FromPlanObject));

        Assert.Empty(route.FromIterator);
    }

    private static PlanOutputRoute Create(params PlanOutputLink[][] columns)
        => PlanOutputRoute.Create([.. columns.Select(c => new PlanOutputPath { Links = c })], PlanObject, Iterator, true);

    private static PlanOutputRoute CreateFromIterator(params PlanOutputLink[][] columns)
        => PlanOutputRoute.Create([.. columns.Select(c => new PlanOutputPath { Links = c })], PlanObject, Iterator, false);

    private static PlanOutputLink[] Column(ulong reference, int offset) =>
    [
        Link(PlanOutputLinkType.OutputList, Outputs, PlanObject, 0x30, EngineLayout.PlanObject.OutputColumnsField, isRoute: true),
        Link(PlanOutputLinkType.OutputArray, OutputArray, Outputs, 0x10, EngineLayout.OutputList.ColumnsField, isRoute: true),
        Link(PlanOutputLinkType.Reference, reference, OutputArray, offset, EngineLayout.OutputList.ColumnField, isRoute: true)
    ];

    private static PlanOutputLink[] Producing(ulong producer,
                                              ulong entry,
                                              ulong iterator,
                                              ulong rowBuffer,
                                              int entryOffset,
                                              ulong reference = Reference,
                                              int depth = 0) =>
    [
        Link(PlanOutputLinkType.Producer, producer, reference, 0x50, depth: depth, isRoute: true),
        Link(PlanOutputLinkType.MapEntry, entry, Map, 0x08, depth: depth, isRoute: true),
        Link(PlanOutputLinkType.Iterator, iterator, entry, 0x10, depth: depth, isRoute: true),
        Link(PlanOutputLinkType.RowBuffer, rowBuffer, iterator, 0x80, depth: depth),
        Link(PlanOutputLinkType.BufferEntry,
             rowBuffer + (ulong)entryOffset,
             rowBuffer,
             entryOffset,
             EngineLayout.RowBuffer.EntryField,
             depth)
    ];

    private static PlanOutputLink Link(PlanOutputLinkType type,
                                       ulong address,
                                       ulong source,
                                       int sourceOffset,
                                       MemoryField? field = null,
                                       int depth = 0,
                                       bool isRoute = false)
        => new(type, address, At(1))
        {
            Source = source,
            SourceOffset = sourceOffset,
            Field = field,
            Depth = depth,
            IsRoute = isRoute
        };

    private static string Describe(IEnumerable<PlanOutputRouteNode> nodes)
        => string.Join(",", nodes.Select(n => $"{n.Link.Type}{(n.Index >= 0 ? $"#{n.Index}" : string.Empty)}"
                                              + (n.Children.Count > 0 ? $"({Describe(n.Children)})" : string.Empty)));
}
