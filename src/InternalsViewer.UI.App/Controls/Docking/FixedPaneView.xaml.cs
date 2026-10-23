using System;
using Windows.ApplicationModel.DataTransfer;
using InternalsViewer.UI.App.ViewModels.Docking;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Docking;

public sealed partial class FixedPaneView : UserControl
{
    public FixedPaneView()
    {
        InitializeComponent();

        Loaded += (_, _) => DockDragState.ActiveChanged += OnDragActiveChanged;

        Unloaded += (_, _) => DockDragState.ActiveChanged -= OnDragActiveChanged;
    }

    public FixedNode? Node { get; private set; }

    public DockLayoutViewModel? Dock { get; private set; }

    private bool AcceptsCurrentDrag => DockDragState.Document is { } document && Dock?.FindGroup(document) is not null;

    public void Initialize(FixedNode node, DockLayoutViewModel dock, FrameworkElement? content)
    {
        Node = node;

        Dock = dock;

        ContentHost.Content = content;
    }

    public void Release() => ContentHost.Content = null;

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
        var document = DockDragState.Document;

        HideHighlight();

        if (document is not null && Node is { } node && Dock is { } dock && dock.FindGroup(document) is not null)
        {
            var zone = GetZone(e);

            if (zone is not DropZone.Center)
            {
                DispatcherQueue.TryEnqueue(() => dock.Move(document, node, zone));
            }
        }

        DockDragState.End();

        e.Handled = true;
    }

    private DropZone GetZone(DragEventArgs e) 
        => DockDropZones.GetZone(e.GetPosition(RootArea), RootArea.ActualWidth, RootArea.ActualHeight);

    private void HideHighlight() => DropHighlight.Visibility = Visibility.Collapsed;
}
