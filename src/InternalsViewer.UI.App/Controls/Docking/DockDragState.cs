using System;
using InternalsViewer.UI.App.ViewModels.Docking;

namespace InternalsViewer.UI.App.Controls.Docking;

/// <summary>
/// Process-wide state for an in-progress tab drag. Because all dock groups live in the same
/// window we can carry the dragged document directly rather than serialising it onto the
/// drag <c>DataPackage</c>. Groups listen to <see cref="ActiveChanged"/> so they can arm their
/// drop overlays only while a drag is happening.
/// </summary>
public static class DockDragState
{
    public static DocumentViewModel? Document { get; private set; }

    public static bool IsActive => Document is not null;

    public static event EventHandler? ActiveChanged;

    public static void Begin(DocumentViewModel document)
    {
        Document = document;
        ActiveChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void End()
    {
        if (Document is null)
        {
            return;
        }

        Document = null;
        ActiveChanged?.Invoke(null, EventArgs.Empty);
    }
}
