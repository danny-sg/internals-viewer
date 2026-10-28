using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace InternalsViewer.UI.App.Controls.Query;

public sealed partial class NoQueryHint : UserControl
{
    public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.Register(nameof(Layout),
                                    typeof(QueryLayoutViewModel),
                                    typeof(NoQueryHint),
                                    new PropertyMetadata(null));

    public QueryLayoutViewModel? Layout
    {
        get => (QueryLayoutViewModel?)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public NoQueryHint()
    {
        InitializeComponent();
    }

    private void OnSqlEditorClick(Hyperlink sender, HyperlinkClickEventArgs args) => Layout?.ShowSqlEditor();
}
