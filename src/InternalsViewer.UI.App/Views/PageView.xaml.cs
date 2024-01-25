using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Messages;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using Windows.UI.Core;
using InternalsViewer.UI.App.ViewModels.Page;
using InternalsViewer.UI.App.Controls.Allocation;

namespace InternalsViewer.UI.App.Views;

public sealed partial class PageView
{
    public PageTabViewModel ViewModel => (PageTabViewModel)DataContext;

    public PageView()
    {
        InitializeComponent();
    }

    private void PageAddressTextBox_Changed(object sender, PageNavigationEventArgs args)
    {
        ViewModel.LoadPageCommand.Execute(new PageAddress(args.FileId, args.PageId));
    }

    private void Control_PageClicked(object sender, PageNavigationEventArgs e)
    {
        var pageAddress = new PageAddress(e.FileId, e.PageId);

        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        
        var isShiftPressed = state.HasFlag(CoreVirtualKeyStates.Down);

        if (isShiftPressed)
        {
            var request = new OpenPageRequest(ViewModel.Database, pageAddress) { Slot = e.Slot };

            WeakReferenceMessenger.Default.Send(new OpenPageMessage(request));
        }
        else
        {
            ViewModel.LoadPageCommand.Execute(pageAddress);
        }
    }

        private void OffsetTableListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        var listView = sender as ListView;

        // If the clicked item is already selected, unselect it
        if (listView?.SelectedItem == e.ClickedItem)
        {
            listView.DeselectAll();
            ViewModel.SelectedSlot = null;
        }
    }
}