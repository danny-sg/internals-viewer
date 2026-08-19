namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreDictionaryTabView
{
    public ColumnstoreDictionaryTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => SubjectText.Text = DataContext?.ToString() ?? string.Empty;
    }
}
