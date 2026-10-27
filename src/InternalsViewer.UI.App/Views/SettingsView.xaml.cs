using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Controls;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Mcp;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace InternalsViewer.UI.App.Views;

public sealed partial class SettingsView : Page
{
    private readonly DispatcherTimer _memoryTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public SettingsView()
    {
        InitializeComponent();

        ViewModel.RefreshMemoryUsage();

        foreach (var client in ViewModel.McpClients)
        {
            McpServerExpander.Items.Add(CreateMcpClientCard(client));
        }

        _memoryTimer.Tick += (_, _) => ViewModel.RefreshMemoryUsage();

        Loaded += (_, _) => _memoryTimer.Start();
        Unloaded += (_, _) => _memoryTimer.Stop();
    }

    internal AppLogViewModel AppLogViewModel { get; } = App.GetService<AppLogViewModel>();

    internal SettingsViewModel ViewModel { get; } = App.GetService<SettingsViewModel>();

#pragma warning disable VSTHRD100
    private async void OpenLogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await WeakReferenceMessenger.Default.Send(new OpenLogMessage());
        }
        catch (Exception exception)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(exception));
        }
    }
#pragma warning restore VSTHRD100

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        AppLogViewModel.ClearLogCommand.Execute(null);
    }

#pragma warning disable VSTHRD100
    private async void BrowseTraceDirectory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await PickFolderAsync() is { } path)
            {
                ViewModel.TraceDirectory = path;
            }
        }
        catch (Exception exception)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(exception));
        }
    }
#pragma warning restore VSTHRD100

#pragma warning disable VSTHRD100
    private async void BrowseSymbolsPath_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await PickFolderAsync() is { } path)
            {
                ViewModel.SymbolsPath = path;
            }
        }
        catch (Exception exception)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(exception));
        }
    }
#pragma warning restore VSTHRD100

    private static SettingsCard CreateMcpClientCard(McpClientViewModel client)
    {
        var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center };

        status.SetBinding(TextBlock.TextProperty,
                          new Binding { Source = client, Path = new PropertyPath(nameof(client.StatusText)), Mode = BindingMode.OneWay });

        if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var brush) && brush is Brush foreground)
        {
            status.Foreground = foreground;
        }

        var register = new Button { Command = client.RegisterCommand };

        register.SetBinding(ContentControl.ContentProperty,
                            new Binding
                            {
                                Source = client,
                                Path = new PropertyPath(nameof(client.RegisterLabel)),
                                Mode = BindingMode.OneWay
                            });

        var remove = new Button { Content = "Remove", Command = client.UnregisterCommand };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        if (client.HelpUrl is { } helpUrl)
        {
            panel.Children.Add(new HyperlinkButton { Content = "Setup Instructions", NavigateUri = helpUrl });
        }

        panel.Children.Add(status);

        panel.Children.Add(register);

        panel.Children.Add(remove);

        return new SettingsCard { Header = client.Name, Description = client.Description, Content = panel };
    }

    private static async Task<string?> PickFolderAsync()
    {
        if (App.MainWindow is null)
        {
            return null;
        }

        var picker = new FolderPicker();

        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();

        return folder?.Path;
    }
}
