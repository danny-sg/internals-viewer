using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Compilation.Enums;

namespace InternalsViewer.Query.FullTrace.Compilation;

public sealed record TraceToken(string Text,
                                int Offset,
                                TraceTokenType TokenType,
                                int Id,
                                CallTreeNode Node,
                                int CallIndex,
                                TracePosition Position)
{
    public string KindName => Enum.IsDefined(TokenType) ? TokenType.ToString() : $"Type {(int)TokenType}";
}
