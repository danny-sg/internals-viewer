using System;
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
using InternalsViewer.UI.App.Controls.Index;

namespace InternalsViewer.UI.App.Views;

public sealed partial class IndexView: IDisposable
{
    private const float MinimumZoom = 0.001f;
    private const float MaximumZoom = 10f;

    public IndexTabViewModel ViewModel => (IndexTabViewModel)DataContext;

    public IndexView()
    {
        InitializeComponent();

        // Clicking on the Index View selects the page and reads the records
        IndexControl.PageClicked += IndexView_PageClicked;

        // Clicking Previous Page or Next Page links loads the previous or next page
        PreviousPageAddressLink.Click += PageAddressLink_OnClick;
        NextPageAddressLink.Click += PageAddressLink_OnClick;

        // Hovering over a Previous or Next page highlights the page to show the double linked list
        PreviousPageAddressLink.PointerEntered += PageAddressLink_PointerEntered;
        PreviousPageAddressLink.PointerExited += PageAddressLink_PointerExited;

        NextPageAddressLink.PointerEntered += PageAddressLink_PointerEntered;
        NextPageAddressLink.PointerExited += PageAddressLink_PointerExited;

        // Displays the page location
        RecordGrid.PageOver += RecordGrid_PageOver;

        // Opens the page from the grid
        RecordGrid.PageClicked += IndexView_PageClicked;
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
        try
        {
            await ViewModel.Refresh();
        }
        catch (Exception ex)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(ex));
        }
    }

    private void RecordGrid_PageOver(object? sender, PageAddressEventArgs e)
    {
        ViewModel.SetHighlightedPage(e.PageAddress);
    }

    private async void IndexView_PageClicked(object? sender, PageAddressEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(ex));
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
        try
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
                await ViewModel.LoadPage(pageAddress);
            }
        }
        catch (Exception ex)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(ex));
        }
    }

    private void IndexView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var maxWidth = e.NewSize.Width / 2;

        // Set max width of the index grid to 50% (grid doesn't accept MaxWidth=50* so we have to do it in code)
        ContainerGrid.ColumnDefinitions[1].MaxWidth = maxWidth;
        IndexGrid.MaxWidth = maxWidth;
    }

    public void Dispose()
    {
        IndexControl.PageClicked -= IndexView_PageClicked;

        PreviousPageAddressLink.Click -= PageAddressLink_OnClick;
        NextPageAddressLink.Click -= PageAddressLink_OnClick;

        PreviousPageAddressLink.PointerEntered -= PageAddressLink_PointerEntered;
        PreviousPageAddressLink.PointerExited -= PageAddressLink_PointerExited;

        NextPageAddressLink.PointerEntered -= PageAddressLink_PointerEntered;
        NextPageAddressLink.PointerExited -= PageAddressLink_PointerExited;

        RecordGrid.PageOver -= RecordGrid_PageOver;

        RecordGrid.PageClicked -= IndexView_PageClicked;

        RecordGrid.Dispose();
        IndexControl.Dispose();
    }
}
