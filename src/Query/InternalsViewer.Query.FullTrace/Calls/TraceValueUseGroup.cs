using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Calls.Enums;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed record TraceValueUseGroup(TraceValueRole Role, List<TraceValueUse> Uses)
{
    public int Calls => Uses.Sum(u => u.Calls);

    public static List<TraceValueUseGroup> Build(IEnumerable<TraceValueUse> uses, Func<TraceValueUse, FunctionType?> getFunctionType)
        => [.. uses.GroupBy(u => u.GetRole(getFunctionType(u)))
                   .OrderBy(g => g.Key)
                   .Select(g => new TraceValueUseGroup(g.Key, [.. g]))];
}
