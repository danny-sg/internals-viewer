using System;
using CommunityToolkit.WinUI.UI.Controls;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Controls.Allocation;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Index;

public class PageAddressLinkButtonColumn<T> : DataGridBoundColumn
{
    public event EventHandler<PageAddressEventArgs>? PageClicked;

    public event EventHandler<PageAddressEventArgs>? PageOver;

    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var button = new HyperlinkButton
        {
            Style = (Style)Application.Current.Resources["PageAddressHyperlinkButtonStyle"],
        };

        if (Binding != null)
        {
            button.SetBinding(ContentControl.ContentProperty, Binding);
        }

        button.Click += (sender, _) => PageClicked?.Invoke(this, new PageAddressEventArgs(GetPageAddress(sender)));

        button.PointerEntered += (sender, _) => PageOver?.Invoke(this, new PageAddressEventArgs(GetPageAddress(sender)));
        button.PointerExited += (_, _) => PageOver?.Invoke(this, new PageAddressEventArgs(PageAddress.Empty));

        return button;
    }

    private static PageAddress GetPageAddress(object sender)
        => ((HyperlinkButton)sender).Content is PageAddress pageAddress ? pageAddress : PageAddress.Empty;

    protected override object PrepareCellForEdit(FrameworkElement editingElement, RoutedEventArgs editingEventArgs)
    {
        return null!;
    }

    protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
    {
        return null!;
    }
}