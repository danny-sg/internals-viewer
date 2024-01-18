using InternalsViewer.Internals.Engine.Address;
using System.Collections.ObjectModel;
using DatabasePage = InternalsViewer.Internals.Engine.Pages.Page;

namespace InternalsViewer.UI.App.vNext.Controls.Page;

public sealed partial class PageHeaderControl
{
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
        control.Values.Add(new PageHeaderValue { Label = "Object Id", Value = header.InternalObjectId.ToString() });
        control.Values.Add(new PageHeaderValue { Label = "Index Id", Value = header.InternalIndexId.ToString() });
        control.Values.Add(new PageHeaderValue { Label = "Allocation Unit Id", Value = header.AllocationUnitId.ToString() });
    }

    public ObservableCollection<PageHeaderValue> Values { get; } = new();


}

public class PageHeaderValue
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}


public class PageAddressHeaderValue: PageHeaderValue
{
    public PageAddress PageAddress { get; set; } = PageAddress.Empty;

    public new string Value => PageAddress.ToString();
}