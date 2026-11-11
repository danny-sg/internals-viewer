using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators.PlanNodes;

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
    public void A_Storage_Column_Is_The_Current_Record_Of_The_Scans_Rowset()
    {
        var memory = Route().Write(Reference + 0x30, Producer)
                            .Write(ProducerIterator + 0x48, Rowset + 0x70)
                            .Write(Rowset + 0xB8, Row)
                            .Write(Row + 0x50, Record);

        var links = Read(memory, "CValRefSS", a => a == Record);

        Assert.Equal([(PlanOutputLinkType.Rowset, Rowset + 0x70, ProducerIterator, 0x48),
                      (PlanOutputLinkType.Row, Row, Rowset, 0xB8),
                      (PlanOutputLinkType.Record, Row + 0x50, Row, 0x50)],
                     links.Where(l => !l.IsRoute).Select(l => (l.Type, l.Address, l.Source, l.SourceOffset)));
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

        var links = Read(memory, "CValRefJoin");

        Assert.Equal([(PlanOutputLinkType.InputValue, InputValue, Cached, 0x08),
                      (PlanOutputLinkType.BufferEntry, RowBuffer + 0x20, InputValue, 0x08)],
                     links.Where(l => l is { Depth: 0, Type: PlanOutputLinkType.InputValue or PlanOutputLinkType.BufferEntry })
                          .Select(l => (l.Type, l.Address, l.Source, l.SourceOffset)));
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

        var links = Read(memory, "CValRefJoin", a => a == Record);

        Assert.Equal((PlanOutputLinkType.Record, Row + 0x50, Row, 0x50, 1, (int?)-1),
                     links.Select(l => (l.Type, l.Address, l.Source, l.SourceOffset, l.Slot, l.Offset)).Last());
    }

    [Fact]
    public void A_Hash_Column_Has_No_Record()
    {
        var memory = JoinRoute().Write(Value + 0x10, Hash)
                                .Write(Hash + 0x2C, 1)
                                .Write(RowContext + 0x08, Rowset)
                                .Write(Rowset, Row)
                                .Write(Row + 0x50, Record);

        var links = Read(memory, "CValRefJoin", _ => true);

        Assert.DoesNotContain(links, l => l.Type == PlanOutputLinkType.Record);
    }

    private static List<PlanOutputLink> Read(FakeTraceReplay memory,
                                             string referenceClass,
                                             Func<ulong, bool>? isRecord = null,
                                             IReadOnlyList<ulong>? planIterators = null)
        => PlanOutputLinkReader.GetLinks(memory,
                                         PlanObject,
                                         Iterator,
                                         Reference,
                                         At(5),
                                         a => GetClassName(a, referenceClass),
                                         a => a switch
                                         {
                                             Map => 0x10,
                                             InputArray => 0x08,
                                             InputValueArray => 0x10,
                                             ProducerIterator => 0x100,
                                             RowBuffer => 0x100,
                                             _ => 0
                                         },
                                         isRecord ?? (_ => false),
                                         planIterators ?? []);

    private static string? GetClassName(ulong address, string referenceClass) => address switch
    {
        Reference => referenceClass,
        Producer => "CXteHash",
        InputReference => "CValRefOrdinal",
        InputProducer => "CXteTableScan",
        Hash => "CValHash",
        Cached => "CValHashCachedSwitch",
        Storage => "CValSSTable",
        _ => null
    };

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
