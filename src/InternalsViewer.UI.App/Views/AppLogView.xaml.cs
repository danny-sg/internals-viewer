using System;
using InternalsViewer.UI.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views;

/// <summary>
/// Standalone tab view for the diagnostic log.
/// </summary>
public sealed partial class AppLogView : UserControl, IDisposable
{
    internal AppLogViewModel ViewModel { get; } = App.GetService<AppLogViewModel>();

    public AppLogView()
    {
        InitializeComponent();
    }

    public void Dispose()
    {
        Bindings.StopTracking();

        LogEntriesList.ItemsSource = null;
    }
}
