using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.ViewModels.Docking;

public sealed partial class TabGroupNode : LayoutNode
{
    [ObservableProperty]
    private DocumentViewModel? _selectedDocument;

    public TabGroupNode()
    {
    }

    public TabGroupNode(params DocumentViewModel[] documents)
    {
        foreach (var document in documents)
        {
            Documents.Add(document);
        }

        SelectedDocument = Documents.Count > 0 ? Documents[0] : null;
    }

    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    public override double GetMinWidth(double splitterSize) => Documents.Count == 0 ? 0 : Documents.Max(d => d.MinWidth);
}
