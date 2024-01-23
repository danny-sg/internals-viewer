using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using InternalsViewer.Internals;
using InternalsViewer.UI.App.Activation;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Connections;
using InternalsViewer.UI.App.ViewModels.Database;
using InternalsViewer.UI.App.ViewModels.Page;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;

namespace InternalsViewer.UI.App;

public partial class App
{
    private IHost Host { get; }

    public static T GetService<T>()
        where T : class
    {
        if ((Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host
                .CreateDefaultBuilder()
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            services.AddSingleton<SettingsService>();

            services.RegisterServices();

            services.Configure<SettingsOptions>(context.Configuration.GetSection(nameof(SettingsOptions)));

            services.AddTransient<ConnectServerViewModelFactory>();
            services.AddTransient<DatabaseTabViewModelFactory>();
            services.AddTransient<PageTabViewModelFactory>();

            services.AddTransient<MainViewModel>();

            services.AddTransient<MainWindow>();
        }).Build();

        UnhandledException += App_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new ExceptionMessage(e.Exception));

        e.Handled = true;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = Host.Services.GetRequiredService<MainWindow>();

        MainWindow.Activate();

        await MainWindow.InitializeAsync();
    }
}
