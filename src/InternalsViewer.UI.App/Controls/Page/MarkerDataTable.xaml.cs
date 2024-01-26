using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.ViewModels.Page;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace InternalsViewer.UI.App.Controls.Page;

public sealed partial class MarkerDataTable : UserControl
{
    public event EventHandler<PageNavigationEventArgs>? PageClicked;

    public PageTabViewModel TabViewModel => (PageTabViewModel)DataContext;

    public MarkerDataTable()
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
