using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.Mcp.Documents;

public sealed class DocumentContext
{
    public DocumentContext(QueryExportContent run)
    {
        Run = run;

        Trace = run.ExtraOf<TraceResult>();

        Nodes = new NodeIndex();

        if (run.Result.CallStackTree is { } tree)
        {
            Nodes.AddTree(tree.Root);
        }

        if (Trace is { } trace && !ReferenceEquals(trace.CallStack.Root, run.Result.CallStackTree?.Root))
        {
            Nodes.AddTree(trace.CallStack.Root);
        }

        Events = new EventIndex(run.Result);
    }

    public QueryExportContent Run { get; }

    public TraceResult? Trace { get; }

    public NodeIndex Nodes { get; }

    public EventIndex Events { get; }

    public Lock CallStoreSync { get; } = new();

    private Lazy<FunctionNames>? Functions { get; set; }

    public string GetFunctionName(CallStackNode node)
    {
        if (node is { Frame: { } frame, Symbol: { Length: > 0 } symbol })
        {
            return $"{frame.Module}!{symbol}";
        }

        if (node.Key.Length > 0)
        {
            return node.Key;
        }

        return node.IsRoot ? "(root)" : $"node {Nodes.GetId(node)}";
    }

    public string GetFunctionName(int nodeId) => Nodes.GetNode(nodeId) is { } node ? GetFunctionName(node) : $"node {nodeId}";

    public string GetFunctionName(ulong functionAddress, ulong instanceAddress)
    {
        Functions ??= new Lazy<FunctionNames>(() => new FunctionNames(this));

        return Functions.Value.Get(functionAddress, instanceAddress);
    }

    private sealed class FunctionNames
    {
        public FunctionNames(DocumentContext context)
        {
            foreach (var node in context.Nodes.GetNodes())
            {
                if (node.Frame is not { } frame)
                {
                    continue;
                }

                var name = context.GetFunctionName(node);

                Exact.TryAdd((frame.Address, frame.InstanceAddress), name);

                Loose.TryAdd(frame.Address, name);

                Loose.TryAdd(frame.Rva, name);
            }
        }

        private Dictionary<(ulong Address, ulong Instance), string> Exact { get; } = [];

        private Dictionary<ulong, string> Loose { get; } = [];

        public string Get(ulong functionAddress, ulong instanceAddress)
            => Exact.GetValueOrDefault((functionAddress, instanceAddress))
               ?? Loose.GetValueOrDefault(functionAddress)
               ?? $"0x{functionAddress:X}";
    }
}
