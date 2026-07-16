using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>
/// A link out of an operator's segment to one next to it in the plan, and a way to go there
/// </summary>
/// <remarks>
/// Scoping to one operator cuts everything around it away, which is the point — but it leaves the segment with nothing
/// to say where the calls at its edges went, and no way back to what drove it. These put the neighbours back as links
/// rather than as their frames: the plan stays walkable from inside the stack, and selecting one scopes to it.
/// </remarks>
/// <param name="Operator">The operator on the other side of the link</param>
/// <param name="Back">Whether this points back at the caller rather than on to what the segment handed off to</param>
public sealed record OperatorLink(ExecutionOperatorEvent Operator, bool Back = false)
{
    // Segoe Fluent Icons: Back and Forward. By code point rather than the character, which is unreadable in source
    // and does not survive every editor.
    private const char BackArrow = (char)0xE72B;

    private const char ForwardArrow = (char)0xE72A;

    /// <summary>
    /// The arrow to draw, pointing the way the link goes
    /// </summary>
    public string Glyph => (Back ? BackArrow : ForwardArrow).ToString();
}
