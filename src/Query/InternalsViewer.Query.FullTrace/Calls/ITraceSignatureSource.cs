using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Calls;

public interface ITraceSignatureSource
{
    Task<ArgumentLayout?> GetLayoutAsync(CallFrame frame);

    Task<FunctionType?> GetFunctionTypeAsync(CallFrame frame);
}
