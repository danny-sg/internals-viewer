using CommunityToolkit.WinUI;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.UI.App.vNext.ViewModels.Tabs;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.vNext.Views;

public sealed partial class PageView
{
    public PageViewModel ViewModel => (PageViewModel)DataContext;

    public PageView()
    {
        InitializeComponent();
    }

    private void PageAddressTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (PageAddressParser.TryParse(PageAddressTextBox.Text, out var pageAddress))
            {
                ViewModel.LoadPageCommand.Execute(pageAddress);
            }
        }
    }

    private void Control_PageClicked(object sender, Controls.Allocation.PageClickedEventArgs e)
    {
        ViewModel.LoadPageCommand.Execute(new PageAddress(e.FileId, e.PageId));
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
