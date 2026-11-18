using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Compilation.Enums;

namespace InternalsViewer.Query.FullTrace.Compilation;

public sealed record TraceToken(string Text,
                                int Offset,
                                TraceTokenKind Kind,
                                int Id,
                                CallTreeNode Node,
                                int CallIndex,
                                TracePosition Position)
{
    public string KindName => Enum.IsDefined(Kind) ? Kind.ToString() : $"Kind {(int)Kind}";
}
