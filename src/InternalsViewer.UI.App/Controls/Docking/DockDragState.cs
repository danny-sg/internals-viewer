using System;
using InternalsViewer.UI.App.ViewModels.Docking;

namespace InternalsViewer.UI.App.Controls.Docking;

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
