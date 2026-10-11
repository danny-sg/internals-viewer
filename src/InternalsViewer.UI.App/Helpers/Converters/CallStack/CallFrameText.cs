using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Helpers.Converters.CallStack;

public static class CallFrameText
{
    public static string ClassPrefix(object? content)
        => content is CallStackNode { Frame: { Signature: not null, Resolved.ClassName: { Length: > 0 } className } }
            ? $"{className}::"
            : string.Empty;

    public static string Signature(object? content)
        => content is CallStackNode node ? node.Frame?.Signature ?? node.Symbol : string.Empty;
}
