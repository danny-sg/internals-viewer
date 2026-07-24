using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed record OperatorLink(ExecutionOperatorEvent Operator, bool Back = false)
{
    private const char BackArrow = (char)0xE72B;

    private const char ForwardArrow = (char)0xE72A;

    public string Glyph => (Back ? BackArrow : ForwardArrow).ToString();
}
