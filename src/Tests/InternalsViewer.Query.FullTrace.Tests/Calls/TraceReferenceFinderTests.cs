using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;

namespace InternalsViewer.Query.FullTrace.Tests.Calls;

[Trait("Category", "Unit")]
public class TraceReferenceFinderTests
{
    private const ulong Value = 0xA0;

    [Fact]
    public async Task A_Value_A_Member_Function_Is_Called_On_Takes_Its_Type_From_The_Class()
    {
        var (store, tree, _) = Create();

        var references = await Finder(store, tree, FunctionType.Member).FindAsync(Value, CancellationToken.None);

        var group = Assert.Single(references.Groups);

        Assert.Equal((TraceValueRole.CalledOn, 2), (group.Role, group.Calls));
        Assert.Equal("CQScanTableScanNew", references.Type);
        Assert.Null(references.Iterator);
    }

    [Fact]
    public async Task A_Static_Function_Is_Passed_The_Value_Rather_Than_Called_On_It()
    {
        var (store, tree, _) = Create();

        var references = await Finder(store, tree, FunctionType.Static).FindAsync(Value, CancellationToken.None);

        Assert.Equal(TraceValueRole.Passed, Assert.Single(references.Groups).Role);
        Assert.Null(references.Type);
    }

    [Fact]
    public async Task An_Iterator_Is_Named_As_One()
    {
        var (store, tree, getRow) = Create();

        var iterator = new IteratorTarget(getRow, null);

        var references = await Finder(store, tree, FunctionType.Member, new Dictionary<ulong, IteratorTarget> { [Value] = iterator })
            .FindAsync(Value, CancellationToken.None);

        Assert.Equal("CQScanTableScanNew Iterator", references.Type);
        Assert.Same(iterator, references.Iterator);
    }

    [Fact]
    public async Task The_Calls_Behind_A_Use_Come_From_The_Same_Search()
    {
        var (store, tree, _) = Create();

        var finder = Finder(store, tree, FunctionType.Member);

        var use = Assert.Single(Assert.Single((await finder.FindAsync(Value, CancellationToken.None)).Groups).Uses);

        var calls = await finder.GetCallsAsync(use, Value, CancellationToken.None);

        Assert.Equal([0, 1], calls.Select(c => c.CallIndex));
    }

    private static (TraceCallStore Store, CallStackTree Tree, CallStackNode GetRow) Create()
    {
        var tree = new CallStackTree();

        var getRow = tree.AddCall(tree.Root, Frame(0x10, "CQScanTableScanNew::GetRow"), 2);

        var builder = new TraceCallStore.Builder();

        builder.Add(getRow.Frame!.Address, 0, Chunk(Row(1, rcx: Value, callNodeIndex: 0), Row(2, rcx: Value, callNodeIndex: 0)), 2);

        var store = builder.Build();

        store.MapNodes([getRow]);

        return (store, tree, getRow);
    }

    private static TraceReferenceFinder Finder(TraceCallStore store,
                                               CallStackTree tree,
                                               FunctionType type,
                                               Dictionary<ulong, IteratorTarget>? iterators = null)
        => new(store, tree, iterators ?? [], HoBtAccessMap.Empty, MemoryMap.Empty, _ => null, new Signatures(type));

    private static CallstackFrame Frame(ulong rva, string symbol) => new()
    {
        Module = "sqlmin",
        Rva = (uint)rva,
        Address = 0x7FF0_0000_0000 + rva,
        Resolved = ResolvedCallstackFrameParser.Parse("sqlmin", symbol)
    };

    private sealed class Signatures(FunctionType type) : ITraceSignatureSource
    {
        public Task<ArgumentLayout?> GetLayoutAsync(CallstackFrame frame) => Task.FromResult<ArgumentLayout?>(null);

        public Task<FunctionType?> GetFunctionTypeAsync(CallstackFrame frame) => Task.FromResult<FunctionType?>(type);
    }
}
