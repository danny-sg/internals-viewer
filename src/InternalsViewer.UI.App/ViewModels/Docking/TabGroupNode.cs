using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.ViewModels.Docking;

/// <summary>A leaf in the dock tree: a tab strip hosting one or more <see cref="DocumentViewModel"/>.</summary>
public sealed partial class TabGroupNode : LayoutNode
{
    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    [ObservableProperty]
    private DocumentViewModel? selectedDocument;

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
}
