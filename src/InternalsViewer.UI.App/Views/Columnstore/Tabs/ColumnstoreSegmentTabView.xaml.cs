namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreSegmentTabView
{
    public ColumnstoreSegmentTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => SubjectText.Text = DataContext?.ToString() ?? string.Empty;
    }
}
