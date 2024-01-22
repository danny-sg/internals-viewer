using InternalsViewer.Internals.Engine.Address;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using DatabasePage = InternalsViewer.Internals.Engine.Pages.Page;
using Windows.ApplicationModel.DataTransfer;
using InternalsViewer.UI.App.Controls.Allocation;
using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Parsers;
using Microsoft.UI.Xaml.Documents;

namespace InternalsViewer.UI.App.Controls.Page;

public sealed partial class PageHeaderControl
{
    public event EventHandler<PageClickedEventArgs>? PageClicked;

    public PageHeaderControl()
    {
        InitializeComponent();

        DataContext = this;
    }

    public DatabasePage Page
    {
        get { return (DatabasePage)GetValue(PageProperty); }
        set { SetValue(PageProperty, value); }
    }

    public static readonly DependencyProperty PageProperty = DependencyProperty
        .Register(nameof(Page),
            typeof(DatabasePage),
            typeof(PageHeaderControl),
            PropertyMetadata.Create(() => "Page", OnPagePropertyChanged));

    private static void OnPagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PageHeaderControl)d;

        var page = (DatabasePage?)e.NewValue;

        control.Values.Clear();

        if (page is null)
        {
            return;
        }

        var header = page.PageHeader;

        control.Values.Add(new PageHeaderValue { Label = "Page Address", Value = header.PageAddress.ToString() });
        control.Values.Add(new PageHeaderValue { Label = "Page Type", Value = header.PageTypeName });

        control.Values.Add(new PageAddressHeaderValue { Label = "Next Page", PageAddress = header.NextPage });
        control.Values.Add(new PageAddressHeaderValue { Label = "Previous Page", PageAddress = header.PreviousPage });

        control.Values.Add(new PageHeaderValue { Label = "Internal Object Id", Value = header.InternalObjectId.ToString() });
        control.Values.Add(new PageHeaderValue { Label = "Internal Index Id", Value = header.InternalIndexId.ToString() });
        control.Values.Add(new PageHeaderValue { Label = "Allocation Unit Id", Value = header.AllocationUnitId.ToString() });

        string? levelTag = null;

        if(header.PageType == Internals.Engine.Pages.Enums.PageType.Index)
        {
            levelTag = header.Level == 0 ? "Leaf" : "Node";
        }  

        control.Values.Add(new PageHeaderValue { Label = "Level", Value = header.Level.ToString(), Tag = levelTag });

        control.Values.Add(new PageHeaderValue { Label = "Slot Count", Value = header.SlotCount.ToString() });

        control.Values.Add(new PageHeaderValue { Label = "Free Count", Value = header.FreeCount.ToString() });

        control.Values.Add(new PageHeaderValue { Label = "Free Data", Value = header.FreeData.ToString() });

        control.Values.Add(new PageHeaderValue { Label = "Reserved Count", Value = header.ReservedCount.ToString() });

        control.Values.Add(new PageHeaderValue { Label = "Transaction Reserved Count", Value = header.TransactionReservedCount.ToString() });

        control.Values.Add(new PageHeaderValue { Label = "Torn Bits", Value = header.TornBits.ToString() });

        control.Values.Add(new PageHeaderValue { Label = "Flag Bits", Value = header.FlagBits.ToString() });

        control.Values.Add(new PageHeaderValue { Label = "LSN", Value = header.Lsn.ToString() });

        control.Values.Add(new PageHeaderValue { Label = "Header Version", Value = header.HeaderVersion.ToString() });

        control.Values.Add(new PageHeaderValue { Label = "Ghost Record Count", Value = header.GhostRecordCount.ToString() });

        control.Values.Add(new PageHeaderValue { Label = "Type Flag Bits", Value = header.TypeFlagBits.ToString() });

        control.Values.Add(new PageHeaderValue { Label = "Internal Transaction Id", Value = header.InternalTransactionId.ToString() });
    }

    public ObservableCollection<PageHeaderValue> Values { get; } = new();

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var tag = ((Button)sender).Tag.ToString();

        var package = new DataPackage();

        package.SetText(tag);

        Clipboard.SetContent(package);
    }

    private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        var value = ((HyperlinkButton)sender).Tag.ToString();

        var pageAddress = PageAddressParser.Parse(value ?? string.Empty);

        PageClicked?.Invoke(this, new PageClickedEventArgs(pageAddress.FileId, pageAddress.PageId));
    }
}

public class PageHeaderValue
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Tag { get; set; }
}

public class PageAddressHeaderValue : PageHeaderValue
{
    public PageAddress PageAddress { get; set; } = PageAddress.Empty;

    public new string Value => PageAddress.ToString();
}