using System;
using Windows.System;
using Windows.UI.Core;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Index;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using InternalsViewer.UI.App.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Index;

public sealed partial class QueryIndexTabView : UserControl, IDisposable
{
    private const float MinimumZoom = 0.001f;
    private const float MaximumZoom = 10f;
    private const float ZoomToPageZoom = 1f;
    private const double StaleRecordsOpacity = 0.4;

    private bool _hasLoaded;

    public QueryIndexTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
        Loaded += OnLoaded;
        PointerWheelChanged += OnPointerWheelChanged;
        IndexControl.PageClicked += OnPageClicked;
        PreviousPageAddressLink.Click += PageAddressLink_OnClick;
        NextPageAddressLink.Click += PageAddressLink_OnClick;

        PreviousPageAddressLink.PointerEntered += PageAddressLink_PointerEntered;
        PreviousPageAddressLink.PointerExited += PageAddressLink_PointerExited;

        NextPageAddressLink.PointerEntered += PageAddressLink_PointerEntered;
        NextPageAddressLink.PointerExited += PageAddressLink_PointerExited;

        RecordGrid.PageOver += RecordGrid_PageOver;
        RecordGrid.PageClicked += OnPageClicked;

        var io = ColourConstants.IoColour;

        IndexControl.SingleSelectedColour = Windows.UI.Color.FromArgb(255, io.R, io.G, io.B);
        IndexControl.RangeSelectedColour = Windows.UI.Color.FromArgb(255, io.R, io.G, io.B);
        IndexControl.SelectedBackgroundColour = Windows.UI.Color.FromArgb(200, io.R, io.G, io.B);
    }

    public IndexTabViewModel? ViewModel => DataContext as IndexTabViewModel;

    public float? ZoomToPageTarget(bool isZoomToPage) => isZoomToPage ? ZoomToPageZoom : null;

    public string FormatPageAddress(PageAddress? pageAddress) => pageAddress?.ToString() ?? string.Empty;

    public double StaleOpacity(bool isStale) => isStale ? StaleRecordsOpacity : 1;

    public Visibility PageTypeVisibility(string? pageType)
        => string.IsNullOrEmpty(pageType) ? Visibility.Collapsed : Visibility.Visible;

    public bool IsPageLinkEnabled(PageAddress? pageAddress) => pageAddress is not null && pageAddress != PageAddress.Empty;

    public void Dispose()
    {
        // x:Bind listens to the view model, which outlives the view, so the view stays rooted until
        // tracking stops
        Bindings.StopTracking();

        IndexControl.PageClicked -= OnPageClicked;
        PreviousPageAddressLink.Click -= PageAddressLink_OnClick;
        NextPageAddressLink.Click -= PageAddressLink_OnClick;

        PreviousPageAddressLink.PointerEntered -= PageAddressLink_PointerEntered;
        PreviousPageAddressLink.PointerExited -= PageAddressLink_PointerExited;

        NextPageAddressLink.PointerEntered -= PageAddressLink_PointerEntered;
        NextPageAddressLink.PointerExited -= PageAddressLink_PointerExited;

        RecordGrid.PageOver -= RecordGrid_PageOver;
        RecordGrid.PageClicked -= OnPageClicked;
        IndexControl.Dispose();

        RecordGrid.Dispose();
    }

#pragma warning disable VSTHRD100
    // ReSharper disable once AsyncVoidMethod
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded || ViewModel is null)
        {
            return;
        }

        _hasLoaded = true;

        try
        {
            await ViewModel.Refresh();
        }
        catch (Exception ex)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(ex));
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var isControlPressed = InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);

        if (!isControlPressed)
        {
            return;
        }

        var newZoom = ViewModel.Zoom + e.GetCurrentPoint(this).Properties.MouseWheelDelta / 4000F;

        if (newZoom is >= MinimumZoom and <= MaximumZoom)
        {
            ViewModel.Zoom = newZoom;
        }
    }

    // ReSharper disable once AsyncVoidEventHandlerMethod
    private async void OnPageClicked(object? sender, PageAddressEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        try
        {
            var pageAddress = new PageAddress(e.FileId, e.PageId);

            if (e.Tag == "Open")
            {
                await WeakReferenceMessenger.Default
                    .Send(new OpenPageMessage(new OpenPageRequest(ViewModel.Database, pageAddress)));
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

    // ReSharper disable once AsyncVoidEventHandlerMethod
    private async void PageAddressLink_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not HyperlinkButton { Content: PageAddress pageAddress })
        {
            return;
        }

        try
        {
            ViewModel.SetHighlightedPage(PageAddress.Empty);

            var isShiftPressed = InputKeyboardSource
                .GetKeyStateForCurrentThread(VirtualKey.Shift)
                .HasFlag(CoreVirtualKeyStates.Down);

            if (isShiftPressed)
            {
                await WeakReferenceMessenger.Default
                    .Send(new OpenPageMessage(new OpenPageRequest(ViewModel.Database, pageAddress)));
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

    #pragma warning restore VSTHRD100

    private void PageAddressLink_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is HyperlinkButton { Content: PageAddress pageAddress })
        {
            ViewModel?.SetHighlightedPage(pageAddress);
        }
    }

    private void PageAddressLink_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        ViewModel?.SetHighlightedPage(PageAddress.Empty);
    }

    private void RecordGrid_PageOver(object? sender, PageAddressEventArgs e)
    {
        ViewModel?.SetHighlightedPage(e.PageAddress);
    }

    private void CloseDetailPane()
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.IsDetailPaneVisible = false;
    }
}
