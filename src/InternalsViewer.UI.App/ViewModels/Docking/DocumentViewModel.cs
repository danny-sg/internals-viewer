using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.ViewModels.Query;

namespace InternalsViewer.UI.App.ViewModels.Docking;

/// <summary>
/// A single tab in the dock. All query documents share one <see cref="QueryViewModel"/>;
/// <see cref="Kind"/> selects which content surface is rendered for the tab.
/// </summary>
public sealed partial class DocumentViewModel : ObservableObject
{
    public DocumentViewModel(DockDocumentKind kind, string title, QueryViewModel query, bool canClose = true)
    {
        Kind = kind;
        Title = title;
        Query = query;
        CanClose = canClose;
    }

    public DockDocumentKind Kind { get; }

    /// <summary>The shared query view model that every document surface binds to.</summary>
    public QueryViewModel Query { get; }

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private bool canClose;
}
