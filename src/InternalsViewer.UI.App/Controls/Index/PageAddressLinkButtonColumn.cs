using System;
using InternalsViewer.Internals.Engine.Address;
using Microsoft.UI.Xaml.Controls;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Controls.Index;

public sealed class PageAddressLinkButtonColumn<T> : TableViewBoundColumn
{
    public event EventHandler<PageAddressEventArgs>? PageClicked;

    public event EventHandler<PageAddressEventArgs>? PageOver;

    public override FrameworkElement GenerateElement(TableViewCell cell, object? dataItem)
    {
        var button = new HyperlinkButton
        {
            Style = (Style)Application.Current.Resources["CellPageAddressStyle"],
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

    protected override object PrepareCellForEdit(TableViewCell cell, RoutedEventArgs editingEventArgs)
    {
        return null!;
    }

    public override FrameworkElement GenerateEditingElement(TableViewCell cell, object? dataItem)
    {
        return null!;
    }
}
