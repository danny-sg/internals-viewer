using System.IO.Compression;
using System.Text.Json.Nodes;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Tests;

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

        var getRow = fullTrace.CallStack.Root.ChildNodes.Single().ChildNodes.Single();

        Assert.Equal(2, calls.Count);
        Assert.Equal(6ul, calls[1].IntegerSlots[0]);
        Assert.Same(getRow, fullTrace.CallStore.GetNode(calls[1]));
        Assert.Equal(new PageAddress(1, 336), calls[1].GetLocator(2)?.Page);
        Assert.Equal("(1:336), (1:336:4)", getRow.LocatorsDetail);
        Assert.Equal(3, fullTrace.Timeline.SpanCount);
        Assert.Equal(100ul, fullTrace.Timeline.Memory.PeakInUseDuring(0, 100));
        Assert.Single(fullTrace.Timeline.Lifetimes);
    }

    [Fact]
    public async Task Imported_Entry_Frames_Point_Into_The_Full_Trace_Tree()
    {
        var imported = await Import(await Export(Content()));

        var scan = Assert.Single(imported.Result.EngineEvents.OfType<ExecutionOperatorEvent>());

        Assert.Same(imported.ExtraOf<TraceResult>()!.CallStack.Root, scan.EntryFrames[0].Parent!.Parent);
    }

    [Fact]
    public async Task A_Node_Removed_From_The_Full_Trace_Tree_Stays_Out_Of_It_On_Import()
    {
        var fullTrace = (await Import(await Export(Content()))).ExtraOf<TraceResult>()!;

        var publish = fullTrace.Timeline.NodeOf(2)!;

        Assert.Equal("XeSqlPkg::lock_acquired::Publish", publish.Symbol);
        Assert.Same(fullTrace.Timeline.NodeOf(0), publish.Parent);
        Assert.DoesNotContain(publish, fullTrace.CallStack.Nodes());
    }

    [Fact]
    public async Task A_Run_Without_A_Full_Trace_Has_No_Full_Trace_Section()
    {
        var export = await Export(Content() with { Extras = [] });

        var imported = await Import(export);

        Assert.DoesNotContain(Entries(export).Keys, k => k.Contains(TraceExportSection.CallTreeName, StringComparison.Ordinal));
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
            Instances = [Scan]
        };

        var fullTrace = FullTrace(scan);

        var result = new QueryResult
        {
            SessionId = "QueryReplay_0123456789abcdef0123456789abcdef",
            IsSuccess = true,
            EngineEvents = [scan],
            CallStackTree = new CallStackTree(),
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
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10, "CQueryScan::GetRow+0x0", 0), 1);

        var getRow = tree.AddCall(caller, Frame(0x30, "CQScanRowsetNew::GetRow+0x0", Scan), 2);

        getRow.Locators = new LocatorSet();

        getRow.Locators.Add(new Locator(0, LocatorValueType.Page, Page336));

        getRow.Locators.Add(new Locator(0, LocatorValueType.Row, Page336 | (4ul << 48)));

        getRow.AllocatedBytes = 100;

        getRow.Allocations = 1;

        getRow.CallActivity = [0, 1, 2];

        scan.EntryFrames = [getRow];

        var publish = tree.AddCall(caller, Frame(0x50, "XeSqlPkg::lock_acquired::Publish+0x0"), 1);

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 0, 100), Span(1, 20, 25), Span(1, 30, 35), Span(2, 40, 41)]);

        var replayed = builder.Build([new(-1, 0, 0x10, 0, 1), new(0, 0, 0x30, Scan, 2), new(0, 0, 0x50, 0, 1)]);

        replayed.MapNodes([caller, getRow, publish]);

        tree.RemoveCallsUnder(node => node.IsExtendedEvents);

        var timeline = replayed.WithoutCallsUnder(node => node.IsExtendedEvents);

        timeline.SetMemory([new MemoryAllocation(7, 22, 23, 100, 0xA)], [new MemoryFree(7, 31, 32, 0xA)]);

        timeline.SetLifetimes(IteratorLifetimeBuilder.Build(timeline, [scan], [], []));

        var storeBuilder = new TraceCallStore.Builder();

        storeBuilder.Add(0x30, Scan, TraceCallRows.Chunk(Call(1, 5), Call(2, 6)), 2);

        storeBuilder.AddLocator(0x30, Scan, 1, new Locator(2, LocatorValueType.Page, Page336), false);

        var callStore = storeBuilder.Build();

        callStore.MapNodes([caller, getRow, publish]);

        return new TraceResult(tree, callStore, timeline);
    }

    private static CallstackFrame Frame(ulong rva, string symbol, ulong instance = 0) => new()
    {
        Module = "sqlmin",
        Pdb = "sqlmin.pdb",
        Guid = "0123456789ABCDEF0123456789ABCDEF",
        Age = 2,
        Rva = (uint)rva,
        Address = 0x7FF0_0000_0000 + rva,
        Instance = instance,
        Resolved = ResolvedCallstackFrameParser.Parse("sqlmin", symbol)
    };

    private static CallSpan Span(int node, ulong start, ulong end)
        => new(start, 0, end, 0, start, end, node, 7, uint.MaxValue, 1);

    private static ulong[] Call(ulong sequence, ulong rcx) => TraceCallRows.Row(sequence, rcx: rcx, node: 1);
}
