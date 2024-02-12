using InternalsViewer.UI.App.ViewModels.Index;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Messages;
using Microsoft.UI.Xaml.Controls;

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

    private void RecordGrid_OnPageOver(object? sender, PageAddressEventArgs e)
    {
        ViewModel.SetHighlightedPage(e.PageAddress);
    }

    private async void IndexView_PageClicked(object? sender, PageAddressEventArgs e)
    {
        var pageAddress = new PageAddress(e.FileId, e.PageId);

        if (e.Tag == "Open")
        {
            await WeakReferenceMessenger.Default.Send(new OpenPageMessage(new OpenPageRequest(ViewModel.Database, pageAddress)));
        }
        else
        {
            await ViewModel.LoadPageCommand.ExecuteAsync(pageAddress);
        }
    }

    private void PageAddressLink_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        var link = (HyperlinkButton)sender; 

        var pageAddress = (PageAddress)link.Content;    

        ViewModel.SetHighlightedPage(pageAddress);
    }

    private void PageAddressLink_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        ViewModel.SetHighlightedPage(PageAddress.Empty);
    }

    private async void PageAddressLink_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SetHighlightedPage(PageAddress.Empty);

        var link = (HyperlinkButton)sender;

        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);

        var isShiftPressed = state.HasFlag(CoreVirtualKeyStates.Down);

        var pageAddress = (PageAddress)link.Content;

        if (isShiftPressed)
        {
            await WeakReferenceMessenger.Default.Send(new OpenPageMessage(new OpenPageRequest(ViewModel.Database, pageAddress)));
        }
        else
        {
            await ViewModel.LoadPageCommand.ExecuteAsync(pageAddress);
        }
    }

    private void IndexView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var maxWidth = e.NewSize.Width / 2;

        // Set max width of the index grid to 50% (grid doesn't accept MaxWidth=50* so we have to do it in code)
        ContainerGrid.ColumnDefinitions[1].MaxWidth = maxWidth;
        IndexGrid.MaxWidth = maxWidth;
    }
}
