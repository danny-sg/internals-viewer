using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.ViewModels.Page;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.ApplicationModel.DataTransfer;

namespace InternalsViewer.UI.App.Controls.Page;

public sealed partial class MarkerDataTable
{
    public event EventHandler<PageNavigationEventArgs>? PageClicked;

    public PageTabViewModel TabViewModel => (PageTabViewModel)DataContext;

    public MarkerDataTable()
    {
        InitializeComponent();
    }

    private void PageLink_Click(object sender, RoutedEventArgs e)
    {
        var value = ((HyperlinkButton)sender).Content.ToString();

        if (value != null)
        {
            var rowIdentifier = RowIdentifier.Parse(value);

            var eventArgs = new PageNavigationEventArgs(rowIdentifier.PageAddress.FileId, rowIdentifier.PageAddress.PageId)
            {
                Slot = rowIdentifier.SlotId
            };

            PageClicked?.Invoke(this, eventArgs);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var value = (sender as CopyButton)?.Tag.ToString() ?? string.Empty;

        var package = new DataPackage();
        
        package.SetText(value);

        Clipboard.SetContent(package);
    }
}
