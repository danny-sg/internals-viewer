using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class SourceFrame(CallStackNode node, ulong moment)
{
    public CallStackNode Node { get; } = node;

    public ulong Moment { get; } = moment;

    public string Symbol => Node.Symbol;
}
