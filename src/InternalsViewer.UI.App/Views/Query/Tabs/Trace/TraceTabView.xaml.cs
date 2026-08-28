using System;
using CommunityToolkit.WinUI;
using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceTabView : UserControl, IDisposable
{
    public TraceTabView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    public TraceTabViewModel? ViewModel => DataContext as TraceTabViewModel;

    public void Dispose()
    {
        DataContextChanged -= OnDataContextChanged;

        Bindings.StopTracking();

        foreach (var child in this.FindChildren())
        {
            if (child is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        (DataContext as TraceTabViewModel)?.Dispose();
    }

#pragma warning disable CA1822
    public string RunLabel(bool isRunning) => isRunning ? "Pause" : "Run";

    public string RunToEndLabel(bool isRunningToEnd) => isRunningToEnd ? "Stop" : "Run to end";

    public string RunGlyph(bool isRunning) => isRunning ? "\uE769" : "\uE768";

    public Visibility BoolVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
#pragma warning restore CA1822

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => Bindings.Update();
}
