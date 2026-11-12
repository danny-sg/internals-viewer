using System;
using InternalsViewer.UI.App.ViewModels.Docking;

namespace InternalsViewer.UI.App.Controls.Docking;

public static class DockDragState
{
    public static event EventHandler? ActiveChanged;
    public static DocumentViewModel? Document { get; private set; }

    public static DockLayoutViewModel? Dock { get; private set; }

    public static bool IsActive => Document is not null;

    public static void Begin(DocumentViewModel document, DockLayoutViewModel dock)
    {
        Document = document;
        Dock = dock;
        ActiveChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void End()
    {
        if (Document is null)
        {
            return;
        }

        Document = null;
        Dock = null;
        ActiveChanged?.Invoke(null, EventArgs.Empty);
    }

    public static bool IsFrom(DockLayoutViewModel? dock) => Dock is not null && ReferenceEquals(Dock, dock);
}
