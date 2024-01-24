using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.UI.App.Messages;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using Windows.UI.Core;
using InternalsViewer.UI.App.ViewModels.Page;
using Windows.ApplicationModel.DataTransfer;
using System;

namespace InternalsViewer.UI.App.Views;

public sealed partial class PageView
{
    public PageTabViewModel ViewModel => (PageTabViewModel)DataContext;

    public PageView()
    {
        InitializeComponent();
    }

    private void PageAddressTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (PageAddressParser.TryParse(PageAddressTextBox.Text, out var pageAddress))
            {
                ViewModel.LoadPageCommand.Execute(pageAddress);
            }
        }
    }

    private void Control_PageClicked(object sender, Controls.Allocation.PageClickedEventArgs e)
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

    private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        var option = int.Parse((sender as MenuFlyoutItem)?.Tag.ToString() ?? string.Empty);

        var text = $"DBCC TRACEON (3604);{Environment.NewLine}" +
            $"DBCC PAGE('{ViewModel.Database.Name}', " +
            $"{ViewModel.Page.PageAddress.FileId}, " +
            $"{ViewModel.Page.PageAddress.PageId}, " +
            $"{option});";
        
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);

    }
}
