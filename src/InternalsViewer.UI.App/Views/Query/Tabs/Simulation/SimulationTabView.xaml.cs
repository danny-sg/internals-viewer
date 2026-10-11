using System;
using System.Windows.Input;
using CommunityToolkit.WinUI;
using InternalsViewer.UI.App.ViewModels.Query.Simulation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Simulation;

public sealed partial class SimulationTabView : UserControl, IDisposable
{
    public SimulationTabView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    public SimulationTabViewModel? ViewModel => DataContext as SimulationTabViewModel;

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

        (DataContext as SimulationTabViewModel)?.Dispose();
    }

#pragma warning disable CA1822
    public string RunLabel(bool isRunning) => isRunning ? "Pause" : "Run";

    public string RunToEndLabel(bool isRunningToEnd) => isRunningToEnd ? "Stop" : "Run to end";

    public string RunGlyph(bool isRunning) => isRunning ? "\uE769" : "\uE768";

    public Visibility BoolVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
#pragma warning restore CA1822

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => Bindings.Update();

    private void OnRunAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => Invoke(ViewModel?.RunCommand, args);

    private void OnRunToEndAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => Invoke(ViewModel?.RunToEndCommand, args);

    private void OnRestartAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => Invoke(ViewModel?.RestartRunCommand, args);

    private void OnPauseAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => Invoke(ViewModel?.PauseCommand, args);

    private static void Invoke(ICommand? command, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }
}
