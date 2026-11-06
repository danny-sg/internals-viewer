using InternalsViewer.Query.FullTrace.Memory.Allocation;
using System.IO.Compression;
using System.Text.Json.Nodes;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Tests.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Plans.Model;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Export;

[Trait("Category", "Unit")]
public class TraceExportSectionTests
{
    private const ulong Scan = 0x9000;

    private const ulong Page336 = 0x0000_0001_0000_0150;

    private static QueryExport WithTrace { get; } = new([new TraceExportSection()]);

    private static QueryExport WithoutTrace { get; } = new([]);

    [Fact]
    public async Task Exporting_An_Imported_Full_Trace_Writes_The_Same_File()
    {
        var first = await Export(Content());

        var second = await Export(await Import(first));

        var firstEntries = Entries(first);

        var secondEntries = Entries(second);

        Assert.Contains(TraceExportSection.CallStoreEntry, firstEntries.Keys);
        Assert.Equal(firstEntries.Keys.Order(), secondEntries.Keys.Order());

        foreach (var (name, content) in firstEntries)
        {
            Assert.True(content.SequenceEqual(secondEntries[name]), $"{name} differs after a round trip");
        }
    }

    [Fact]
    public async Task An_Imported_Full_Trace_Has_Its_Calls_Spans_And_Memory()
    {
        var fullTrace = (await Import(await Export(Content()))).ExtraOf<TraceResult>()!;

        var calls = fullTrace.CallStore.GetCallList(0x30, Scan)!;

        var getRow = fullTrace.CallTree.Root.ChildNodes.Single().ChildNodes.Single();

        Assert.Equal(2, calls.Count);
        Assert.Equal(6ul, calls[1].IntegerSlots[0]);
        Assert.Same(getRow, fullTrace.CallStore.GetNode(calls[1]));
        Assert.Equal(new PageAddress(1, 336), calls[1].GetLocator(2)?.Page);
        Assert.Equal("(1:336), (1:336:4)", getRow.LocatorsDetail);
        Assert.Equal(3, fullTrace.Timeline.SpanCount);
        Assert.Equal(100ul, fullTrace.Allocation.PeakInUseDuring(At(0), At(100)));
        Assert.Single(fullTrace.Lifetimes);
    }

    [Fact]
    public async Task A_Node_Removed_From_The_Full_Trace_Tree_Stays_Out_Of_It_On_Import()
    {
        var fullTrace = (await Import(await Export(Content()))).ExtraOf<TraceResult>()!;

        var publish = fullTrace.Timeline.NodeOf(2)!;

        Assert.Equal("XeSqlPkg::lock_acquired::Publish", publish.Symbol);
        Assert.Same(fullTrace.Timeline.NodeOf(0), publish.Parent);
        Assert.DoesNotContain(publish, fullTrace.CallTree.Nodes());
    }

    [Fact]
    public async Task A_Run_Without_A_Full_Trace_Has_No_Full_Trace_Section()
    {
        var export = await Export(Content() with { Extras = [] });

        var imported = await Import(export);

        Assert.DoesNotContain(TraceExportSection.CallTreeEntry, Entries(export).Keys);
        Assert.Null(imported.ExtraOf<TraceResult>());
    }

    [Fact]
    public async Task A_Full_Trace_Is_Skipped_When_Its_Section_Is_Not_Registered()
    {
        using var stream = new MemoryStream(await Export(Content()));

        var imported = await WithoutTrace.ReadAsync(stream, _ => null, CancellationToken.None);

        Assert.Empty(imported.Extras);
        Assert.Single(imported.Result.EngineEvents.OfType<ExecutionOperatorEvent>());
    }

    private static async Task<byte[]> Export(QueryExportContent content)
    {
        using var stream = new MemoryStream();

        await WithTrace.WriteAsync(stream, content, CancellationToken.None);

        return stream.ToArray();
    }

    private static async Task<QueryExportContent> Import(byte[] export)
    {
        using var stream = new MemoryStream(export);

        return await WithTrace.ReadAsync(stream, _ => null, CancellationToken.None);
    }

    private static Dictionary<string, byte[]> Entries(byte[] export)
    {
        using var zip = new ZipArchive(new MemoryStream(export), ZipArchiveMode.Read);

        var entries = new Dictionary<string, byte[]>();

        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();

            using var copy = new MemoryStream();

            stream.CopyTo(copy);

            entries[entry.FullName] = entry.FullName == QueryExport.ManifestEntry ? WithoutExportTime(copy.ToArray()) : copy.ToArray();
        }

        return entries;
    }

    private static byte[] WithoutExportTime(byte[] manifest)
    {
        var json = JsonNode.Parse(manifest)!.AsObject();

        json.Remove("ExportedAt");

        return System.Text.Encoding.UTF8.GetBytes(json.ToJsonString());
    }

    private static QueryExportContent Content()
    {
        var scan = new ExecutionOperatorEvent
        {
            Name = "Clustered Index Scan",
            OperatorDescription = "Clustered Index Scan",
            PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = 0 },
            Threads = [new OperatorThread(0, 10, 20, 5)],
        };

        var fullTrace = FullTrace(scan);

        var result = new QueryResult
        {
            SessionId = "QueryReplay_0123456789abcdef0123456789abcdef",
            IsSuccess = true,
            EngineEvents = [scan],
            CropStartUs = 10,
            CropEndUs = 30
        };

        return new QueryExportContent
        {
            ServerName = ".\\SQL2025",
            DatabaseName = "Sales",
            SqlText = "SELECT * FROM dbo.Orders",
            Result = result,
            Extras = [fullTrace]
        };
    }

    private static TraceResult FullTrace(ExecutionOperatorEvent scan)
    {
        var tree = new CallTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10, "CQueryScan::GetRow+0x0", 0), 1);

        var getRow = tree.AddCall(caller, Frame(0x30, "CQScanRowsetNew::GetRow+0x0", Scan), 2);

        getRow.Locators = new LocatorSet();

        getRow.Locators.Add(new Locator(0, LocatorValueType.Page, Page336));

        getRow.Locators.Add(new Locator(0, LocatorValueType.Row, Page336 | (4ul << 48)));

        getRow.AllocatedBytes = 100;

        getRow.Allocations = 1;

        getRow.CallActivity = [0, 1, 2];

        var publish = tree.AddCall(caller, Frame(0x50, "XeSqlPkg::lock_acquired::Publish+0x0"), 1);

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 0, 100), Span(1, 20, 25), Span(1, 30, 35), Span(2, 40, 41)]);

        var timeline = builder.Build([new(-1, 0, 0x10, 0, 1), new(0, 0, 0x30, Scan, 2), new(0, 0, 0x50, 0, 1)]);

        timeline.MapNodes([caller, getRow, publish]);

        tree.RemoveCallsUnder(node => node.IsInstrumentation);

        timeline.RemoveCallsUnder(node => node.IsInstrumentation);

        var memory = AllocationIndex.Build([new MemoryAllocation(7, At(22), At(23), 100, 0xA)], [new MemoryFree(7, At(31), At(32), 0xA)]);

        IteratorPlanNode[] planNodes = [new(Scan, PlanObject: 0xA000, BatchPlanObject: 0, Query: 0xB000, NodeId: 0)];

        var owners = new Dictionary<ulong, ExecutionOperatorEvent> { [Scan] = scan };

        var lifetimes = IteratorLifetimeBuilder.Build(timeline, memory, owners, planNodes, []);

        var storeBuilder = new TraceCallStore.Builder();

        storeBuilder.Add(0x30, Scan, TraceCallRowHelper.Chunk(Call(1, 5), Call(2, 6)), 2);

        storeBuilder.AddLocator(0x30, Scan, 1, new Locator(2, LocatorValueType.Page, Page336), false);

        var callStore = storeBuilder.Build();

        callStore.MapNodes([caller, getRow, publish]);

        return new TraceResult(tree, callStore, timeline) { Allocation = memory, Lifetimes = lifetimes, PlanNodes = planNodes };
    }

    private static CallFrame Frame(ulong rva, string symbol, ulong instance = 0)
    {
        SymbolsHelper.SplitOffset(symbol, out var name, out var offset);

        return new CallFrame
        {
            Module = new("sqlmin", "sqlmin.pdb", "0123456789ABCDEF0123456789ABCDEF", 2),
            Rva = (uint)rva,
            Address = 0x7FF0_0000_0000 + rva,
            InstanceAddress = instance,
            Function = FrameFunction.Parse("sqlmin", name),
            Offset = offset
        };
    }

    private static CallSpan Span(int callNodeIndex, ulong start, ulong end)
        => new(start, 0, end, 0, start, end, callNodeIndex, 7, uint.MaxValue, 1);

    private static ulong[] Call(ulong sequence, ulong rcx) => TraceCallRowHelper.Row(sequence, rcx: rcx, callNodeIndex: 1);
}
