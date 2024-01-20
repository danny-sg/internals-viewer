﻿using Microsoft.UI.Xaml.Controls;
using InternalsViewer.UI.App.vNext.Controls.Page;

namespace InternalsViewer.UI.App.vNext.Helpers.Selectors;

public class PageHeaderTemplateSelector : DataTemplateSelector
{
    public DataTemplate ValueTemplate { get; set; } = null!;

    public DataTemplate PageAddressTemplate { get; set; } = null!;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return item switch
        {
            PageAddressHeaderValue => PageAddressTemplate,
            PageHeaderValue => ValueTemplate,
            _ => null
        };
    }
}