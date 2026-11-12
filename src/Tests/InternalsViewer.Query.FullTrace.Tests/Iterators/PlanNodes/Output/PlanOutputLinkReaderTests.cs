using InternalsViewer.Query.FullTrace.Iterators;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators.PlanNodes.Output;

[Trait("Category", "Unit")]
public class PlanOutputLinkReaderTests
{
    private const ulong PlanObject = 0x1F4_0000_1000;

    private const ulong Outputs = 0x1F4_0000_1100;

    private const ulong OutputArray = 0x1F4_0000_1200;

    private const ulong Reference = 0x1F4_0000_1300;

    private const ulong Producer = 0x1F4_0000_1400;

    private const ulong InputReference = 0x1F4_0000_1500;

    private const ulong InputProducer = 0x1F4_0000_1600;

    private const ulong Iterator = 0x1F4_0000_2000;

    private const ulong ProducerIterator = 0x1F4_0000_2400;

    private const ulong InputIterator = 0x1F4_0000_2800;

    private const ulong Context = 0x1F4_0000_3000;

    private const ulong QueryContext = 0x1F4_0000_3400;

    private const ulong Map = 0x1F4_0000_3800;

    private const ulong Entry = 0x1F4_0000_3900;

    private const ulong OtherEntry = 0x1F4_0000_3A00;

    private const ulong Values = 0x1F4_0000_4000;

    private const ulong Value = 0x1F4_0000_4100;

    private const ulong Hash = 0x1F4_0000_4200;

    private const ulong Cached = 0x1F4_0000_4300;

    private const ulong Storage = 0x1F4_0000_4400;

    private const ulong QueryScan = 0x1F4_0000_5000;

    private const ulong ScanContext = 0x1F4_0000_5100;

    private const ulong Statement = 0x1F4_0000_5200;

    private const ulong RowContext = 0x1F4_0000_5300;

    private const ulong RowBuffer = 0x1F4_0000_6000;

    private const ulong Accessor = 0x1F4_0000_7000;

    private const ulong Bindings = 0x1F4_0000_7100;

    private const ulong Rowset = 0x1F4_0000_7200;

    private const ulong Row = 0x1F4_0000_7300;

    private const ulong Record = 0x1F4_0001_0060;

    private const ulong OwnerEntry = 0x1F4_0000_3B00;

    private const ulong OwnerIterator = 0x1F4_0000_2C00;

    private const ulong Profiling = 0x1F4_0000_2E00;

    private const ulong InputArray = 0x1F4_0000_8000;

    private const ulong InputValueArray = 0x1F4_0000_8100;

    private const ulong InputValue = 0x1F4_0000_8200;

    private const ulong Project = 0x1F4_0000_C000;

    private const ulong Expression = 0x1F4_0000_C100;

    private const ulong Operands = 0x1F4_0000_C200;

    private const ulong Adapter = 0x1F4_0000_D000;

    private const ulong AdapterValues = 0x1F4_0000_D100;

    private const ulong AdapterValue = 0x1F4_0000_D200;

    private const ulong AdapterBuffer = 0x1F4_0000_D300;

    private const ulong GroupColumns = 0x1F4_0000_F000;

    private const ulong AggregateColumns = 0x1F4_0000_F100;

    private const ulong CountColumns = 0x1F4_0000_F200;

    private const ulong VariableTable = 0x1F4_0000_F300;

    private const ulong Variables = 0x1F4_0000_F400;

    private const string StreamAggregate = "CXteStreamAggregate";

    [Fact]
    public void Each_Link_Carries_The_Engine_Field_It_Was_Read_From()
    {
        var memory = JoinRoute().Write(Value + 0x10, Hash)
                                .Write(Hash + 0x10, 4)
                                .Write(Hash + 0x2C, 2);

        var links = Read(memory, "CValRefJoin");

        Assert.Same(EngineLayout.QueryContext.IteratorMapField, links.Single(l => l.Type == PlanOutputLinkType.IteratorMap).Field);

        Assert.Same(EngineLayout.HashIterator.ProbeOutputValuesField,
                    links.Single(l => l.Type == PlanOutputLinkType.OutputValues).Field);

        Assert.Same(EngineLayout.HashValue.SlotField,
                    links.Single(l => l is { Type: PlanOutputLinkType.RowContext, IsRoute: false }).Field);
    }

    [Fact]
    public void A_Hash_Join_Column_Follows_The_Engine_Route_To_Its_Row_Slot()
    {
        var memory = JoinRoute().Write(Value + 0x10, Hash)
                                .Write(Hash + 0x10, 4)
                                .Write(Hash + 0x2C, 2);

        var links = Read(memory, "CValRefJoin");

        Assert.Equal([(PlanOutputLinkType.OutputList, Outputs, PlanObject, 0x30, true),
                      (PlanOutputLinkType.OutputArray, OutputArray, Outputs, 0x10, true),
                      (PlanOutputLinkType.Reference, Reference, OutputArray, 0x08, true),
                      (PlanOutputLinkType.ExecutionContext, Context, Iterator, 0x18, true),
                      (PlanOutputLinkType.QueryContext, QueryContext, Context, 0x1B8, true),
                      (PlanOutputLinkType.IteratorMap, Map, QueryContext, 0x290, true),
                      (PlanOutputLinkType.Producer, Producer, Reference, 0x50, true),
                      (PlanOutputLinkType.MapEntry, Entry, Map, 0x08, true),
                      (PlanOutputLinkType.Iterator, ProducerIterator, Entry, 0x10, true),
                      (PlanOutputLinkType.OutputValues, Values, ProducerIterator, 0x60, false),
                      (PlanOutputLinkType.Value, Value, Values, 0x08, false),
                      (PlanOutputLinkType.Reader, Hash, Value, 0x10, false),
                      (PlanOutputLinkType.QueryScan, QueryScan, QueryContext, 0x20, true),
                      (PlanOutputLinkType.QueryScanContext, ScanContext, QueryScan, 0x58, true),
                      (PlanOutputLinkType.Statement, Statement, ScanContext, 0x28, true),
                      (PlanOutputLinkType.RowContext, RowContext, Statement, 0xD8, true),
                      (PlanOutputLinkType.RowContext, RowContext, Hash, 0x2C, false),
                      (PlanOutputLinkType.RowSlot, RowContext + 0x10, RowContext, 0x10, false)],
                     links.Select(l => (l.Type, l.Address, l.Source, l.SourceOffset, l.IsRoute)));

        var reader = links.Single(l => l.Type == PlanOutputLinkType.Reader);

        Assert.Equal((2, (int?)4), (reader.Slot, reader.Offset));
    }

    [Fact]
    public void A_Hash_Join_Input_Column_Is_Followed_Into_The_Child_Operator()
    {
        var memory = JoinRoute().Write(Value + 0x10, Hash)
                                .Write(Hash + 0x2C, 2)
                                .Write(Value + 0x18, InputReference)
                                .Write(InputReference + 0x50, InputProducer)
                                .Write(InputReference + 0x58, 1)
                                .Write(Map, OtherEntry)
                                .Write(OtherEntry + 0x08, InputProducer)
                                .Write(OtherEntry + 0x10, InputIterator)
                                .Write(InputIterator + 0x80, RowBuffer);

        var links = Read(memory, "CValRefJoin");

        Assert.Equal([(PlanOutputLinkType.Input, InputReference, Value, 0x18, 0),
                      (PlanOutputLinkType.Producer, InputProducer, InputReference, 0x50, 1),
                      (PlanOutputLinkType.MapEntry, OtherEntry, Map, 0x00, 1),
                      (PlanOutputLinkType.Iterator, InputIterator, OtherEntry, 0x10, 1),
                      (PlanOutputLinkType.RowBuffer, RowBuffer, InputIterator, 0x80, 1),
                      (PlanOutputLinkType.BufferEntry, RowBuffer + 0x20, RowBuffer, 0x20, 1)],
                     links.SkipWhile(l => l.Type != PlanOutputLinkType.Input)
                          .Select(l => (l.Type, l.Address, l.Source, l.SourceOffset, l.Depth)));
    }

    [Fact]
    public void A_Hash_Join_Input_Column_Is_A_Stage_Feeding_The_Join()
    {
        var memory = JoinRoute().Write(Value + 0x10, Hash)
                                .Write(Hash + 0x2C, 2)
                                .Write(Value + 0x18, InputReference)
                                .Write(InputReference + 0x50, InputProducer)
                                .Write(InputReference + 0x58, 1)
                                .Write(Map, OtherEntry)
                                .Write(OtherEntry + 0x08, InputProducer)
                                .Write(OtherEntry + 0x10, InputIterator)
                                .Write(InputIterator + 0x80, RowBuffer);

        var stages = GetPath(memory, "CValRefJoin").Stages;

        Assert.Equal([(Reference, 0, -1, Producer, ProducerIterator, PlanOutputStageType.RowContext, RowContext),
                      (InputReference, 1, 0, InputProducer, InputIterator, PlanOutputStageType.RowBuffer, RowBuffer)],
                     stages.Select(s => (s.Reference, s.Depth, s.Consumer, s.PlanObject, s.Iterator, s.Type, s.Address)));

        Assert.Equal((PlanOutputLinkType.BufferEntry, RowBuffer + 0x20), (stages[1].Value?.Type, stages[1].Value?.Address));
    }

    [Fact]
    public void A_Hash_Join_Input_Value_Points_Into_The_Child_Row_Buffer()
    {
        var memory = JoinRoute().Write(Value + 0x10, Hash)
                                .Write(Hash + 0x2C, 2)
                                .Write(Value + 0x18, InputReference)
                                .Write(InputReference + 0x50, InputProducer)
                                .Write(InputReference + 0x58, 1)
                                .Write(Map, OtherEntry)
                                .Write(OtherEntry + 0x08, InputProducer)
                                .Write(OtherEntry + 0x10, InputIterator)
                                .Write(InputIterator + 0x80, RowBuffer)
                                .Write(ProducerIterator + 0xC8, InputValueArray)
                                .Write(InputValueArray + 0x08, InputValue)
                                .Write(InputValue + 0x08, RowBuffer + 0x20);

        var links = Read(memory, "CValRefJoin");

        Assert.Equal([(PlanOutputLinkType.InputValues, InputValueArray, ProducerIterator, 0xC8),
                      (PlanOutputLinkType.InputValue, InputValue, InputValueArray, 0x08),
                      (PlanOutputLinkType.BufferEntry, RowBuffer + 0x20, InputValue, 0x08)],
                     links.Where(l => l.Type is PlanOutputLinkType.InputValues or PlanOutputLinkType.InputValue || l.Source == InputValue)
                          .Select(l => (l.Type, l.Address, l.Source, l.SourceOffset)));
    }

    [Fact]
    public void A_Passed_Through_Column_Is_Reached_Through_The_Inputs_Of_The_Operator()
    {
        var memory = Route().Write(Map, OwnerEntry)
                            .Write(OwnerEntry + 0x08, PlanObject)
                            .Write(OwnerEntry + 0x10, OwnerIterator)
                            .Write(OwnerIterator + 0x08, InputArray)
                            .Write(InputArray, Profiling)
                            .Write(Profiling + 0x10, Producer)
                            .Write(Profiling + 0xA0, ProducerIterator)
                            .Write(ProducerIterator + 0x10, Producer)
                            .Write(Reference + 0x58, 0)
                            .Write(ProducerIterator + 0x80, RowBuffer);

        var links = Read(memory, "CValRefOrdinal");

        Assert.Equal([(PlanOutputLinkType.Inputs, InputArray, OwnerIterator, 0x08),
                      (PlanOutputLinkType.ProfilingIterator, Profiling, InputArray, 0x00),
                      (PlanOutputLinkType.Iterator, ProducerIterator, Profiling, 0xA0),
                      (PlanOutputLinkType.RowBuffer, RowBuffer, ProducerIterator, 0x80),
                      (PlanOutputLinkType.BufferEntry, RowBuffer, RowBuffer, 0x00)],
                     links.Where(l => !l.IsRoute).Select(l => (l.Type, l.Address, l.Source, l.SourceOffset)));
    }

    [Fact]
    public void A_Storage_Column_Is_Linked_Through_The_Scans_Rowset_And_Row_To_Its_Record()
    {
        var memory = Route().Write(Reference + 0x30, Producer)
                            .Write(Reference + 0x58, 2)
                            .Write(ProducerIterator + 0x48, Rowset + 0x70)
                            .Write(Rowset + 0xB8, Row)
                            .Write(Row + 0x50, Record);

        var links = Read(memory, "CValRefSS");

        Assert.Equal([(PlanOutputLinkType.Rowset, Rowset + 0x70, ProducerIterator, 0x48),
                      (PlanOutputLinkType.Row, Row, Rowset, 0xB8),
                      (PlanOutputLinkType.Record, Row + 0x50, Row, 0x50)],
                     links.Where(l => l.Type is PlanOutputLinkType.Rowset or PlanOutputLinkType.Row or PlanOutputLinkType.Record)
                          .Select(l => (l.Type, l.Address, l.Source, l.SourceOffset)));
    }

    [Fact]
    public void A_Storage_Column_Reads_Its_Table_Column_From_The_Scans_Current_Record()
    {
        var memory = Route().Write(Reference + 0x30, Producer)
                            .Write(Reference + 0x58, 2)
                            .Write(ProducerIterator + 0x48, Rowset + 0x70)
                            .Write(Rowset + 0xB8, Row)
                            .Write(Row + 0x50, Record);

        var stage = GetPath(memory, "CValRefSS").Stages[0];

        var source = stage.Record;

        Assert.Equal((PlanOutputLinkType.Record, ProducerIterator, 2), (source?.Type, source?.Address, source?.ColumnId));

        Assert.Equal(Record, source?.Follow(memory));

        Assert.Equal((PlanOutputStageType.Record, ProducerIterator), (stage.Type, stage.Iterator));
    }

    [Fact]
    public void A_Calculated_Column_Follows_The_Columns_Its_Expression_Reads()
    {
        var memory = Route().Write(Reference + 0x58, Project)
                            .Write(Reference + 0x68, Expression)
                            .Write(Expression + 0x28, Operands)
                            .Write(Expression + 0x30, 1)
                            .Write(Operands, InputReference + 0x28)
                            .Write(InputReference + 0x50, InputProducer)
                            .Write(InputReference + 0x58, 1)
                            .Write(Map, OtherEntry)
                            .Write(OtherEntry + 0x08, InputProducer)
                            .Write(OtherEntry + 0x10, InputIterator)
                            .Write(InputIterator + 0x80, RowBuffer);

        var links = Read(memory, "CValRefCalculated");

        Assert.Equal([(PlanOutputLinkType.Expression, Expression, Reference, 0x68, 0),
                      (PlanOutputLinkType.Input, InputReference, Operands, 0x00, 0),
                      (PlanOutputLinkType.Producer, InputProducer, InputReference, 0x50, 1)],
                     links.Where(l => l.Type is PlanOutputLinkType.Expression or PlanOutputLinkType.Input or PlanOutputLinkType.Producer)
                          .Select(l => (l.Type, l.Address, l.Source, l.SourceOffset, l.Depth)));

        Assert.Equal((PlanOutputLinkType.BufferEntry, RowBuffer + 0x20), links.Select(l => (l.Type, l.Address)).Last());
    }

    [Fact]
    public void A_Calculated_Column_Is_An_Expression_Stage_Fed_By_The_Columns_It_Reads()
    {
        var memory = Route().Write(Reference + 0x58, Project)
                            .Write(Reference + 0x68, Expression)
                            .Write(Expression + 0x28, Operands)
                            .Write(Expression + 0x30, 1)
                            .Write(Operands, InputReference + 0x28)
                            .Write(InputReference + 0x50, InputProducer)
                            .Write(InputReference + 0x58, 1)
                            .Write(Map, OtherEntry)
                            .Write(OtherEntry + 0x08, InputProducer)
                            .Write(OtherEntry + 0x10, InputIterator)
                            .Write(InputIterator + 0x80, RowBuffer);

        var stages = GetPath(memory, "CValRefCalculated").Stages;

        Assert.Equal([(0, -1, PlanOutputStageType.Expression, Expression),
                      (1, 0, PlanOutputStageType.RowBuffer, RowBuffer)],
                     stages.Select(s => (s.Depth, s.Consumer, s.Type, s.Address)));
    }

    [Fact]
    public void An_Iterator_Missing_From_The_Map_Is_Found_Through_Its_Profiling_Iterator()
    {
        var memory = Route().Write(Entry + 0x08, InputProducer)
                            .Write(Profiling + 0x10, Producer)
                            .Write(Profiling + 0x90, ProducerIterator)
                            .Write(ProducerIterator + 0x10, Producer)
                            .Write(Reference + 0x58, 0)
                            .Write(ProducerIterator + 0x80, RowBuffer);

        var links = Read(memory, "CValRefOrdinal", planIterators: [Profiling]);

        Assert.Contains(links,
                        l => l is { Type: PlanOutputLinkType.Iterator, Address: ProducerIterator, Source: Profiling, SourceOffset: 0x90 });

        Assert.Equal((PlanOutputLinkType.RowBuffer, RowBuffer, ProducerIterator),
                     links.Where(l => !l.IsRoute).Select(l => (l.Type, l.Address, l.Source)).First());
    }

    [Fact]
    public void A_Probe_Column_Read_From_The_Probe_Row_Buffer_Points_Into_It()
    {
        var memory = JoinRoute().Write(Value + 0x10, Cached)
                                .Write(Cached + 0x08, InputValue)
                                .Write(InputValue + 0x08, RowBuffer + 0x20)
                                .Write(Value + 0x18, InputReference)
                                .Write(InputReference + 0x50, InputProducer)
                                .Write(InputReference + 0x58, 1)
                                .Write(Map, OtherEntry)
                                .Write(OtherEntry + 0x08, InputProducer)
                                .Write(OtherEntry + 0x10, InputIterator)
                                .Write(InputIterator + 0x80, RowBuffer);

        var path = GetPath(memory, "CValRefJoin");

        Assert.Equal([(PlanOutputLinkType.InputValue, InputValue, Cached, 0x08),
                      (PlanOutputLinkType.BufferEntry, RowBuffer + 0x20, InputValue, 0x08)],
                     path.Links.Where(l => l is { Depth: 0, Type: PlanOutputLinkType.InputValue or PlanOutputLinkType.BufferEntry })
                               .Select(l => (l.Type, l.Address, l.Source, l.SourceOffset)));

        Assert.Equal((PlanOutputStageType.InputValue, InputValue), (path.Stages[0].Type, path.Stages[0].Address));
    }

    [Fact]
    public void A_Row_Buffer_That_Is_Not_A_Recorded_Object_Is_Not_Followed()
    {
        var memory = Route().Write(Reference + 0x58, 0)
                            .Write(ProducerIterator + 0x80, 0x2_0000_0001UL);

        var links = Read(memory, "CValRefOrdinal");

        Assert.DoesNotContain(links, l => l.Type is PlanOutputLinkType.RowBuffer or PlanOutputLinkType.BufferEntry);
    }

    [Fact]
    public void A_Batch_Column_Is_Followed_Through_The_Batch_To_Row_Adapter_Into_Its_Row_Buffer()
    {
        var links = Read(BatchRoute(), "CValRefBatchRedirect", planIterators: [Adapter]);

        Assert.Equal([(PlanOutputLinkType.Iterator, ProducerIterator, Adapter, 0x58),
                      (PlanOutputLinkType.OutputValues, AdapterValues, Adapter, 0x48),
                      (PlanOutputLinkType.Value, AdapterValue, AdapterValues, 0x08),
                      (PlanOutputLinkType.RowBuffer, AdapterBuffer, Adapter, 0x78),
                      (PlanOutputLinkType.BufferEntry, AdapterBuffer + 0x20, AdapterBuffer, 0x20)],
                     links.Where(l => !l.IsRoute).Select(l => (l.Type, l.Address, l.Source, l.SourceOffset)));
    }

    [Fact]
    public void A_Batch_Column_Value_Is_The_Adapters_Row_Buffer_Entry()
    {
        var stage = GetPath(BatchRoute(), "CValRefBatchRedirect", planIterators: [Adapter]).Stages[0];

        var value = Assert.NotNull(stage.Value);

        Assert.Equal((PlanOutputLinkType.BufferEntry, AdapterBuffer + 0x20), (value.Type, value.Address));

        Assert.Empty(value.Chain);

        Assert.Equal((PlanObject, Adapter, PlanOutputStageType.RowBuffer), (stage.PlanObject, stage.Iterator, stage.Type));
    }

    [Fact]
    public void A_Hash_Join_Column_Value_Is_Read_Through_Its_Row_Slot()
    {
        var memory = JoinRoute().Write(Value + 0x10, Hash)
                                .Write(Hash + 0x10, 4)
                                .Write(Hash + 0x2C, 2);

        var value = Assert.NotNull(GetPath(memory, "CValRefJoin").Stages[0].Value);

        Assert.Equal((PlanOutputLinkType.RowSlot, RowContext + 0x10, (int?)4), (value.Type, value.Address, value.Offset));

        Assert.Equal([0UL], value.Chain);
    }

    [Fact]
    public void A_Producer_Further_Along_A_Bucket_Is_Reached_Through_The_Next_Entry()
    {
        var memory = Route().Write(Map + 0x08, OtherEntry)
                            .Write(OtherEntry, Entry)
                            .Write(OtherEntry + 0x08, InputProducer)
                            .Write(Reference + 0x58, 0)
                            .Write(ProducerIterator + 0x80, RowBuffer);

        var links = Read(memory, "CValRefOrdinal");

        Assert.Equal([(PlanOutputLinkType.MapEntry, OtherEntry, Map, 0x08),
                      (PlanOutputLinkType.MapEntry, Entry, OtherEntry, 0x00),
                      (PlanOutputLinkType.Iterator, ProducerIterator, Entry, 0x10)],
                     links.Where(l => l.Type is PlanOutputLinkType.MapEntry or PlanOutputLinkType.Iterator)
                          .Select(l => (l.Type, l.Address, l.Source, l.SourceOffset)));
    }

    [Fact]
    public void A_Stream_Aggregate_Group_Column_Is_Its_Variable_In_The_Current_Group_Row()
    {
        var memory = StreamAggregateRoute().Write(GroupColumns, Reference);

        var path = GetPath(memory, "CValRefOrdinal", producerClass: StreamAggregate);

        Assert.Equal([(PlanOutputLinkType.PlanObject, Producer, ProducerIterator, 0x10),
                      (PlanOutputLinkType.GroupColumns, GroupColumns, Producer, 0x98),
                      (PlanOutputLinkType.Reference, Reference, GroupColumns, 0),
                      (PlanOutputLinkType.VariableTable, VariableTable, ProducerIterator, 0xD0),
                      (PlanOutputLinkType.Variables, Variables, VariableTable, 0x08),
                      (PlanOutputLinkType.BufferEntry, Variables, Variables, 0)],
                     path.Links.TakeLast(6).Select(l => (l.Type, l.Address, l.Source, l.SourceOffset)));

        Assert.Equal((PlanOutputLinkType.BufferEntry, Variables), (path.Stages[0].Value?.Type, path.Stages[0].Value?.Address));

        Assert.Equal((PlanOutputStageType.Variables, Variables), (path.Stages[0].Type, path.Stages[0].Address));
    }

    [Fact]
    public void A_Stream_Aggregate_Draws_Only_The_Route_From_Its_Iterator_To_The_Value()
    {
        var memory = StreamAggregateRoute().Write(GroupColumns, Reference);

        var path = GetPath(memory, "CValRefOrdinal", producerClass: StreamAggregate);

        Assert.Equal([PlanOutputLinkType.VariableTable, PlanOutputLinkType.Variables, PlanOutputLinkType.BufferEntry],
                     path.Links.Where(l => !l.IsRoute).Select(l => l.Type));
    }

    [Fact]
    public void A_Stream_Aggregate_Aggregate_Is_The_Variable_After_The_Group_Columns()
    {
        var memory = StreamAggregateRoute().Write(GroupColumns, Producer)
                                           .Write(AggregateColumns, Reference);

        var path = GetPath(memory, "CValRefOrdinal", producerClass: StreamAggregate);

        Assert.Equal([(PlanOutputLinkType.AggregateColumns, AggregateColumns, Producer, 0xA0),
                      (PlanOutputLinkType.Reference, Reference, AggregateColumns, 0),
                      (PlanOutputLinkType.VariableTable, VariableTable, ProducerIterator, 0xD0),
                      (PlanOutputLinkType.Variables, Variables, VariableTable, 0x08),
                      (PlanOutputLinkType.BufferEntry, Variables + 0x20, Variables, 0x20)],
                     path.Links.TakeLast(5).Select(l => (l.Type, l.Address, l.Source, l.SourceOffset)));

        Assert.Equal((PlanOutputLinkType.BufferEntry, Variables + 0x20), (path.Stages[0].Value?.Type, path.Stages[0].Value?.Address));
    }

    [Fact]
    public void A_Stream_Aggregate_Count_Is_The_Value_Held_In_The_Iterator()
    {
        var memory = StreamAggregateRoute().Write(CountColumns, Reference);

        var path = GetPath(memory, "CValRefOrdinal", producerClass: StreamAggregate);

        Assert.Equal([(PlanOutputLinkType.PlanObject, Producer, ProducerIterator, 0x10),
                      (PlanOutputLinkType.CountColumns, CountColumns, Producer, 0xB0),
                      (PlanOutputLinkType.Reference, Reference, CountColumns, 0),
                      (PlanOutputLinkType.BufferEntry, ProducerIterator + 0x88, ProducerIterator, 0x88)],
                     path.Links.TakeLast(4).Select(l => (l.Type, l.Address, l.Source, l.SourceOffset)));

        var stage = path.Stages[0];

        Assert.Equal((PlanOutputLinkType.BufferEntry, ProducerIterator + 0x88), (stage.Value?.Type, stage.Value?.Address));

        Assert.Equal((PlanOutputStageType.Counter, ProducerIterator), (stage.Type, stage.Address));
    }

    [Fact]
    public void A_Stream_Aggregate_Column_Past_The_Reported_Count_Is_Not_Found()
    {
        var memory = StreamAggregateRoute().Write(Producer + 0x60, 0)
                                           .Write(GroupColumns, Reference);

        Assert.Null(GetPath(memory, "CValRefOrdinal", producerClass: StreamAggregate).Stages[0].Value);
    }

    [Fact]
    public void A_Stream_Aggregate_Second_Count_Has_No_Value()
    {
        var memory = StreamAggregateRoute().Write(CountColumns + 0x08, Reference);

        Assert.Null(GetPath(memory, "CValRefOrdinal", producerClass: StreamAggregate).Stages[0].Value);
    }

    [Fact]
    public void A_Scan_Column_Is_The_Entry_For_Its_Column_In_The_Row_Buffer()
    {
        var memory = Route().Write(Reference + 0x58, 2)
                            .Write(ProducerIterator + 0x80, RowBuffer);

        var links = Read(memory, "CValRefOrdinal");

        Assert.Equal([(PlanOutputLinkType.RowBuffer, RowBuffer, ProducerIterator, 0x80),
                      (PlanOutputLinkType.BufferEntry, RowBuffer + 0x40, RowBuffer, 0x40)],
                     links.TakeLast(2).Select(l => (l.Type, l.Address, l.Source, l.SourceOffset)));
    }

    [Fact]
    public void A_Scan_Column_Is_Linked_To_The_Column_Binding_That_Fills_Its_Entry()
    {
        var bindings = new byte[0xA0];

        bindings[0x50 + 0x30] = 0x20;

        var memory = Route().Write(Reference + 0x58, 1)
                            .Write(ProducerIterator + 0x80, RowBuffer)
                            .Write(ProducerIterator + 0x88, Accessor)
                            .Write(Accessor + 0x62, (ushort)2)
                            .Write(Accessor + 0x70, Bindings)
                            .Write(Bindings, bindings);

        var links = Read(memory, "CValRefOrdinal");

        Assert.Equal([(PlanOutputLinkType.BufferEntry, RowBuffer + 0x20, RowBuffer, 0x20),
                      (PlanOutputLinkType.RowBindings, Accessor, ProducerIterator, 0x88),
                      (PlanOutputLinkType.ColumnBindings, Bindings, Accessor, 0x70),
                      (PlanOutputLinkType.ColumnBinding, Bindings + 0x50, Bindings, 0x50)],
                     links.TakeLast(4).Select(l => (l.Type, l.Address, l.Source, l.SourceOffset)));
    }

    [Fact]
    public void A_Probe_Column_Reads_The_Probe_Row_Through_Its_Cached_Storage_Value()
    {
        var memory = JoinRoute().Write(Value + 0x10, Cached)
                                .Write(Cached + 0x08, Storage)
                                .Write(Storage + 0x10, 1);

        var links = Read(memory, "CValRefJoin");

        Assert.Equal([(PlanOutputLinkType.Reader, Cached, Value, 0x10, -1),
                      (PlanOutputLinkType.Reader, Storage, Cached, 0x08, 1),
                      (PlanOutputLinkType.RowSlot, RowContext + 0x08, RowContext, 0x08, 1)],
                     links.Where(l => l.Type is PlanOutputLinkType.Reader or PlanOutputLinkType.RowSlot)
                          .Select(l => (l.Type, l.Address, l.Source, l.SourceOffset, l.Slot)));
    }

    [Fact]
    public void A_Storage_Column_Reads_The_Current_Record_Of_The_Rowset_In_Its_Row_Slot()
    {
        var memory = JoinRoute().Write(Value + 0x10, Storage)
                                .Write(Storage + 0x10, 1)
                                .Write(Storage + 0x18, Accessor)
                                .Write(Accessor + 0x62, (ushort)1)
                                .Write(Accessor + 0x70, Bindings)
                                .Write(Bindings, new byte[0x50])
                                .Write(Bindings, -1)
                                .Write(RowContext + 0x08, Rowset)
                                .Write(Rowset, Row)
                                .Write(Row + 0x50, Record);

        var stage = GetPath(memory, "CValRefJoin").Stages[0];

        Assert.Equal((PlanOutputStageType.RowContext, RowContext), (stage.Type, stage.Address));

        var record = Assert.NotNull(stage.Record);

        Assert.Equal((RowContext + 0x08, (int?)-1), (record.Address, record.Offset));

        Assert.Equal([0UL, 0UL, 0x50UL], record.Chain);
    }

    [Fact]
    public void A_Hash_Column_Has_No_Record()
    {
        var memory = JoinRoute().Write(Value + 0x10, Hash)
                                .Write(Hash + 0x2C, 1)
                                .Write(RowContext + 0x08, Rowset)
                                .Write(Rowset, Row)
                                .Write(Row + 0x50, Record);

        Assert.Null(GetPath(memory, "CValRefJoin").Stages[0].Record);
    }

    private static FakeTraceReplay BatchRoute()
        => Route().Write(Reference + 0x38, PlanObject)
                  .Write(Reference + 0x40, 1)
                  .Write(Adapter + 0x10, PlanObject)
                  .Write(Adapter + 0x48, AdapterValues)
                  .Write(AdapterValues + 0x08, AdapterValue)
                  .Write(Adapter + 0x58, ProducerIterator)
                  .Write(Adapter + 0x78, AdapterBuffer);

    private static IReadOnlyList<PlanOutputLink> Read(FakeTraceReplay memory,
                                                      string referenceClass,
                                                      IReadOnlyList<ulong>? planIterators = null)
        => GetPath(memory, referenceClass, planIterators).Links;

    private static PlanOutputPath GetPath(FakeTraceReplay memory,
                                          string referenceClass,
                                          IReadOnlyList<ulong>? planIterators = null,
                                          string producerClass = "CXteHash")
        => PlanOutputLinkReader.GetPath(memory,
                                        PlanObject,
                                        Iterator,
                                        Reference,
                                        At(5),
                                        a => a == Producer ? producerClass : GetClassName(a, referenceClass),
                                        a => a switch
                                        {
                                            Map => 0x10,
                                            InputArray => 0x08,
                                            InputValueArray => 0x10,
                                            ProducerIterator => 0x118,
                                            RowBuffer => 0x100,
                                            GroupColumns => 0x08,
                                            AggregateColumns => 0x08,
                                            CountColumns => 0x10,
                                            VariableTable => 0x10,
                                            Variables => 0x40,
                                            _ => 0
                                        },
                                        planIterators ?? []);

    private static string? GetClassName(ulong address, string referenceClass) => address switch
    {
        Reference => referenceClass,
        Adapter => "CQScanBatchHelper",
        Producer => "CXteHash",
        InputReference => "CValRefOrdinal",
        InputProducer => "CXteTableScan",
        Hash => "CValHash",
        Cached => "CValHashCachedSwitch",
        Storage => "CValSSTable",
        _ => null
    };

    private static FakeTraceReplay StreamAggregateRoute() => Route().Write(Producer + 0x60, 1)
                                                                   .Write(Producer + 0x68, 1)
                                                                   .Write(Producer + 0x6C, 2)
                                                                   .Write(Producer + 0x98, GroupColumns)
                                                                   .Write(Producer + 0xA0, AggregateColumns)
                                                                   .Write(Producer + 0xB0, CountColumns)
                                                                   .Write(ProducerIterator + 0xD0, VariableTable)
                                                                   .Write(VariableTable + 0x08, Variables);

    private static FakeTraceReplay JoinRoute() => Route().Write(Reference + 0x58, (ushort)1)
                                                         .Write(Reference + 0x5C, 1)
                                                         .Write(ProducerIterator + 0x60, Values)
                                                         .Write(Values + 0x08, Value);

    private static FakeTraceReplay Route() => new FakeTraceReplay().Write(PlanObject + 0x30, Outputs)
                                                                   .Write(Outputs + 0x08, 2)
                                                                   .Write(Outputs + 0x10, OutputArray)
                                                                   .Write(OutputArray + 0x08, Reference)
                                                                   .Write(Reference + 0x50, Producer)
                                                                   .Write(Iterator + 0x18, Context)
                                                                   .Write(Context + 0x1B8, QueryContext)
                                                                   .Write(QueryContext + 0x290, Map)
                                                                   .Write(Map + 0x08, Entry)
                                                                   .Write(Entry + 0x08, Producer)
                                                                   .Write(Entry + 0x10, ProducerIterator)
                                                                   .Write(QueryContext + 0x20, QueryScan)
                                                                   .Write(QueryScan + 0x58, ScanContext)
                                                                   .Write(ScanContext + 0x28, Statement)
                                                                   .Write(Statement + 0xD8, RowContext);
}
