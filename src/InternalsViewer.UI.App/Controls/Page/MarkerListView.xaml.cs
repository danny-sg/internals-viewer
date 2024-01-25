using System;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.ViewModels.Page;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Page;

public sealed partial class MarkerListView
{
    public event EventHandler<PageNavigationEventArgs>? PageClicked;

    public PageTabViewModel TabViewModel => (PageTabViewModel)DataContext;

    public MarkerListView()
    {
        InitializeComponent();
    }

    private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
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
}
