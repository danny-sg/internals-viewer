using System;
using Windows.System;
using Windows.UI.Core;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Page;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using InternalsViewer.UI.App.Controls;

namespace InternalsViewer.UI.App.Views;

public sealed partial class PageView : IDisposable
{
    public PageTabViewModel ViewModel => (PageTabViewModel)DataContext;

    public Visibility GetTabContentVisibility(int selectedIndex, bool isTabVisible, int index)
    {
        return selectedIndex == index && isTabVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public PageView()
    {
        InitializeComponent();

        PageAddressTextBox.AddressChanged += PageAddressTextBox_Changed;
        MarkerTreeView.PageClicked += Control_PageClicked;
        AllocationControl.PageClicked += Control_PageClicked;
        LogRecordTreeView.RecordClicked += OnLogRecordClicked;
        ResultsGrid.PageClicked += Control_PageClicked;
    }

    private void OnLogRecordClicked(Models.LogRecordItem item)
    {
        ViewModel.SelectSlotForRecord(item);
    }

    private void PageAddressTextBox_Changed(object? sender, PageAddressEventArgs args)
    {
        ViewModel.LoadPageCommand.Execute(new PageAddress(args.FileId, args.PageId));
    }

    private void PfsControl_PageClicked(object? sender, PageAddressEventArgs e)
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);

        var isShiftPressed = state.HasFlag(CoreVirtualKeyStates.Down);

        if (isShiftPressed)
        {
            var pageAddress = new PageAddress(e.FileId, e.PageId + ViewModel.AllocationStartPage);

            var request = new OpenPageRequest(ViewModel.Database, pageAddress) { Slot = e.Slot };

            WeakReferenceMessenger.Default.Send(new OpenPageMessage(request));
        }
        else
        {
            var pageAddress = new PageAddress(e.FileId, e.PageId);

            ViewModel.SelectPfsPageCommand.Execute(pageAddress);
        }
    }

    private void Control_PageClicked(object? sender, PageAddressEventArgs e)
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
            if (e.Slot != null)
            {
                ViewModel.LoadRowIdentifierCommand.Execute(new RowIdentifier(pageAddress, e.Slot.Value));
            }
            else
            {
                ViewModel.LoadPageCommand.Execute(pageAddress);
            }
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

    public void Dispose()
    {
        PageAddressTextBox.AddressChanged -= PageAddressTextBox_Changed;
        MarkerTreeView.PageClicked -= Control_PageClicked;
        AllocationControl.PageClicked -= Control_PageClicked;
        LogRecordTreeView.RecordClicked -= OnLogRecordClicked;
        ResultsGrid.PageClicked -= Control_PageClicked;
    }
}
