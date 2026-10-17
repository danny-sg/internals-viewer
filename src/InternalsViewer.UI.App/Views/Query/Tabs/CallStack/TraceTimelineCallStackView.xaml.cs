using System;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class TraceTimelineCallStackView : UserControl, IDisposable
{
    public TraceTimelineCallStackView()
    {
        InitializeComponent();
    }

    public PlayheadCallStackViewModel? ViewModel => DataContext as PlayheadCallStackViewModel;

    private SettingsViewModel Settings { get; } = App.GetService<SettingsViewModel>();

    public void Dispose() => Stack.Dispose();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Settings.TraceTimelineShowCallStack = false;

    private void OnStepBackClick(object sender, RoutedEventArgs e) => ViewModel?.Step(forward: false);

    private void OnStepForwardClick(object sender, RoutedEventArgs e) => ViewModel?.Step(forward: true);
}
