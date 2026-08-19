namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreDeleteBitmapTabView
{
    public ColumnstoreDeleteBitmapTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => SubjectText.Text = DataContext?.ToString() ?? string.Empty;
    }
}
