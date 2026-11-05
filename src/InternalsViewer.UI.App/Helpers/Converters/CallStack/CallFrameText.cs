using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Helpers.Converters.CallStack;

public static class CallFrameText
{
    public static string ClassPrefix(object? content)
        => content is CallStackNode { Frame.Function: { Signature: not null, ClassName: { Length: > 0 } className } }
            ? $"{className}::"
            : string.Empty;

    public static string Signature(object? content)
        => content is CallStackNode node ? node.Frame?.Function?.Signature ?? node.Symbol : string.Empty;
}
