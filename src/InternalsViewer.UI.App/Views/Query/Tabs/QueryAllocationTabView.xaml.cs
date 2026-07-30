using Windows.System;
using Windows.UI.Core;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

public sealed partial class QueryAllocationTabView : UserControl
{
    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    public QueryAllocationTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
        AllocationItemRepeater.SizeChanged += OnParentSizeChanged;
    }

    private void OnParentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var itemCount = viewModel.DatabaseFiles.Length;

        if (itemCount > 0)
        {
            viewModel.AllocationMapHeight = AllocationItemRepeater.ActualHeight / itemCount;
        }
    }

    public string RunLabel(bool isRunning) => isRunning ? "Stop" : "Run";

    public string RunGlyph(bool isRunning) => isRunning ? "" : "";

    public Visibility EmptyStepsVisibility(int count, bool isRunningToEnd)
        => count == 0 && !isRunningToEnd ? Visibility.Visible : Visibility.Collapsed;

    private void CloseTracePane()
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.AllocationTrace.IsTraceVisible = false;
        }
    }

    private void OnPageSelected(object? sender, PageAddressEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var pageAddress = new PageAddress(e.FileId, e.PageId);

        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);

        var isShiftPressed = state.HasFlag(CoreVirtualKeyStates.Down);

        // Shift opens the page as a separate top level tab; a plain click opens it as a document inside the
        // query view's dock layout
        if (isShiftPressed)
        {
            WeakReferenceMessenger.Default
                                  .Send(new OpenPageMessage(new OpenPageRequest(viewModel.Database, pageAddress)
                                  {
                                      LogRecords = viewModel.GetPageLogRecords(pageAddress)
                                  }));
        }
        else
        {
            viewModel.OpenPage(pageAddress);
        }
    }
}
