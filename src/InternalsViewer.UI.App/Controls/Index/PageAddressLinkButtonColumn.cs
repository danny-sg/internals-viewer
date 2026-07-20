using System;
using CommunityToolkit.Mvvm.Input;
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
        var pageAddress = PageAddress.Empty;

        if (Binding != null)
        {
            var value = Binding.Path.Path;
            var propertyValue = dataItem.GetType().GetProperty(value)?.GetValue(dataItem);

            pageAddress = (PageAddress)(propertyValue ?? pageAddress);
        }

        var button = new HyperlinkButton
        {
            Content = pageAddress.ToString(),
            Style = (Style)Application.Current.Resources["PageAddressHyperlinkButtonStyle"],
            Command = new RelayCommand(() =>
            {
                PageClicked?.Invoke(this, new PageAddressEventArgs(pageAddress));
            }),
        };

        button.PointerEntered += (_, _) => PageOver?.Invoke(this, new PageAddressEventArgs(pageAddress));
        button.PointerExited += (_, _) => PageOver?.Invoke(this, new PageAddressEventArgs(PageAddress.Empty));

        return button;
    }

    protected override object PrepareCellForEdit(FrameworkElement editingElement, RoutedEventArgs editingEventArgs)
    {
        return null!;
    }

    protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
    {
        return null!;
    }
}