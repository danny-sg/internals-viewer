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
            PropertyMetadata.Create(() => "Page"));

}
