using InternalsViewer.Query.FullTrace.Calls.Tree;

namespace InternalsViewer.UI.App.Helpers.Converters.Calls;

public static class CallFrameText
{
    public static string ClassPrefix(object? content)
        => content is CallTreeNode { Frame.Function: { Signature: not null, ClassName: { Length: > 0 } className } }
            ? $"{className}::"
            : string.Empty;

    public static string Signature(object? content)
        => content is CallTreeNode node ? node.Frame?.Function?.Signature ?? node.Symbol : string.Empty;
}
