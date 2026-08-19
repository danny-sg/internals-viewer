namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreDeltaStoreTabView
{
    public ColumnstoreDeltaStoreTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => SubjectText.Text = DataContext?.ToString() ?? string.Empty;
    }
}
