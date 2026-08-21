using System;
using Windows.System;
using Windows.UI.Core;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using InternalsViewer.UI.App.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

public sealed partial class QueryAllocationTabView : UserControl, IDisposable
{
    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    public QueryAllocationTabView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        AllocationItemRepeater.SizeChanged += OnParentSizeChanged;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => Bindings.Update();

    public void Dispose()
    {
        DataContextChanged -= OnDataContextChanged;
        AllocationItemRepeater.SizeChanged -= OnParentSizeChanged;

        Bindings.StopTracking();

        foreach (var child in AllocationItemRepeater.FindChildren())
        {
            if (child is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
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
