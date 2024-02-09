using InternalsViewer.UI.App.ViewModels.Index;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Messages;

namespace InternalsViewer.UI.App.Views;

public sealed partial class IndexView
{
    private const float MinimumZoom = 0.001f;
    private const float MaximumZoom = 10f;

    public IndexTabViewModel ViewModel => (IndexTabViewModel)DataContext;

    public IndexView()
    {
        InitializeComponent();
    }

    private void IndexView_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);

        var isControlPressed = state.HasFlag(CoreVirtualKeyStates.Down);

        if (isControlPressed)
        {
            var newZoom = ViewModel.Zoom + e.GetCurrentPoint(this).Properties.MouseWheelDelta / 4000F;

            if (newZoom is >= MinimumZoom and <= MaximumZoom)
            {
                ViewModel.Zoom = newZoom;
            }
        }
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.Initialize();
    }

    private void IndexControl_PageClicked(object sender, Controls.Allocation.PageNavigationEventArgs e)
    {
        var pageAddress = new PageAddress(e.FileId, e.PageId);

        WeakReferenceMessenger.Default.Send(new OpenPageMessage(new OpenPageRequest(ViewModel.Database, pageAddress)));
    }
}
