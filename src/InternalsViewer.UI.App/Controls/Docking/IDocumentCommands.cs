namespace InternalsViewer.UI.App.Controls.Docking;

/// <summary>
/// A document view that builds its own tab strip commands
/// </summary>
/// <remarks>
/// Commands that only read the document's view model are better declared as a standalone control and passed to
/// <c>DocumentViewModel</c> as a factory. This interface is for the cases where the controls have to reach back into
/// the view — the timeline's transport, the call stack's navigation — which a separately constructed element cannot.
/// Either way a fresh element is built for each strip that asks: moving one element between strips is what WinUI
/// refuses to do, whatever it is first detached from.
/// </remarks>
public interface IDocumentCommands
{
    FrameworkElement? CreateCommands();
}
