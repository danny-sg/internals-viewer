using System;
using Windows.ApplicationModel.DataTransfer;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Docking;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Docking;

public sealed partial class FixedPaneView : UserControl
{
    public FixedPaneView()
    {
        InitializeComponent();

        LayoutHelpers.ClipToBounds(ContentHost);

        Loaded += (_, _) =>
        {
            DockDragState.ActiveChanged -= OnDragActiveChanged;

            DockDragState.ActiveChanged += OnDragActiveChanged;
        };

        Unloaded += (_, _) =>
        {
            if (!IsLoaded)
            {
                DockDragState.ActiveChanged -= OnDragActiveChanged;
            }
        };
    }

    public FixedNode? Node { get; private set; }

    public DockLayoutViewModel? Dock { get; private set; }

    private bool AcceptsCurrentDrag => DockDragState.IsFrom(Dock);

    public void Initialize(FixedNode node, DockLayoutViewModel dock, FrameworkElement? content)
    {
        Node = node;

        Dock = dock;

        ContentHost.Content = content;
    }

    private void OnDragActiveChanged(object? sender, EventArgs e)
    {
        if (!DockDragState.IsActive)
        {
            HideHighlight();
        }
    }

    private void OnDropOver(object sender, DragEventArgs e)
    {
        if (!AcceptsCurrentDrag)
        {
            return;
        }

        var zone = GetZone(e);

        if (zone is DropZone.Center)
        {
            e.AcceptedOperation = DataPackageOperation.None;

            HideHighlight();
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.Move;

            DockDropZones.Place(DropHighlight, zone, RootArea.ActualWidth, RootArea.ActualHeight);
        }

        if (e.DragUIOverride is not null)
        {
            e.DragUIOverride.IsCaptionVisible = false;

            e.DragUIOverride.IsGlyphVisible = false;
        }

        e.Handled = true;
    }

    private void OnDropLeave(object sender, DragEventArgs e) => HideHighlight();

    private void OnDrop(object sender, DragEventArgs e)
    {
        HideHighlight();

        if (!AcceptsCurrentDrag || DockDragState.Document is not { } document || Node is not { } node || Dock is not { } dock)
        {
            return;
        }

        var zone = GetZone(e);

        if (zone is not DropZone.Center)
        {
            DispatcherQueue.TryEnqueue(() => dock.Move(document, node, zone));
        }

        DockDragState.End();

        e.Handled = true;
    }

    private DropZone GetZone(DragEventArgs e) 
        => DockDropZones.GetZone(e.GetPosition(RootArea), RootArea.ActualWidth, RootArea.ActualHeight);

    private void HideHighlight() => DropHighlight.Visibility = Visibility.Collapsed;
}
