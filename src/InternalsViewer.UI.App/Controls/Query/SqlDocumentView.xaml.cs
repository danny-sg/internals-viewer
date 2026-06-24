using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Query;

/// <summary>Dock document hosting the SQL editor for the active query.</summary>
public sealed partial class SqlDocumentView : UserControl
{
    public QueryViewModel? ViewModel => (DataContext as DocumentViewModel)?.Query ?? DataContext as QueryViewModel;

    public SqlDocumentView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }
}
