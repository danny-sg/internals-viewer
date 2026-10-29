using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Calls;

public interface ITraceSignatureSource
{
    Task<ArgumentLayout?> GetLayoutAsync(CallstackFrame frame);

    Task<FunctionType?> GetFunctionTypeAsync(CallstackFrame frame);
}
