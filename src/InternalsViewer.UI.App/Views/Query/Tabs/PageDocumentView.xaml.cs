using System;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>
/// Dock document hosting a page view inside the query view
/// </summary>
/// <remarks>
/// The document's content is a PageTabViewModel, inherited by the inner PageView through the DataContext - the same view/view model pair
/// as the top level page tab, just docked in the query layout instead
/// </remarks>
public sealed partial class PageDocumentView : UserControl, IDisposable
{
    public PageDocumentView()
    {
        InitializeComponent();
    }

    public void Dispose()
    {
        PageView.Dispose();
    }
}
