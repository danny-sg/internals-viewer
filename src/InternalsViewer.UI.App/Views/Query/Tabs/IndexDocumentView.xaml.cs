using System;
using Windows.System;
using Windows.UI.Core;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Index;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

public sealed partial class IndexDocumentView : UserControl, IDisposable
{
    private const float MinimumZoom = 0.001f;
    private const float MaximumZoom = 10f;

    private bool _hasLoaded;

    public IndexTabViewModel? ViewModel => DataContext as IndexTabViewModel;

    public IndexDocumentView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
        Loaded += OnLoaded;
        PointerWheelChanged += OnPointerWheelChanged;
        IndexControl.PageClicked += OnPageClicked;

        var io = ColourConstants.IoColour;

        IndexControl.SingleSelectedColour = Windows.UI.Color.FromArgb(255, io.R, io.G, io.B);
        IndexControl.RangeSelectedColour = Windows.UI.Color.FromArgb(255, io.R, io.G, io.B);
        IndexControl.SelectedBackgroundColour = Windows.UI.Color.FromArgb(200, io.R, io.G, io.B);
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
    #pragma warning restore VSTHRD100

    public void Dispose()
    {
        IndexControl.PageClicked -= OnPageClicked;
        IndexControl.Dispose();
    }
}
