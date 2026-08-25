using System;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Connection.BackupFile.Connection;
using InternalsViewer.Execution;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Services.Logging;
using InternalsViewer.Query;
using InternalsViewer.Query.Events;
using InternalsViewer.TransactionLog;
using InternalsViewer.UI.App.Activation;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.Services.XEvents;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using InternalsViewer.UI.App.ViewModels.Connections;
using InternalsViewer.UI.App.ViewModels.Database;
using InternalsViewer.UI.App.ViewModels.Index;
using InternalsViewer.UI.App.ViewModels.Page;
using InternalsViewer.UI.App.ViewModels.Query;
using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace InternalsViewer.UI.App;

public partial class App
{
    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions
                        .Hosting
                        .Host
                        .CreateDefaultBuilder()
                        .UseContentRoot(AppContext.BaseDirectory)
                        .ConfigureLogging(logging =>
                        {
                            logging.ClearProviders();
                            logging.SetMinimumLevel(LogLevel.Trace);
                            logging.AddFilter("Microsoft", LogLevel.Warning);
                            logging.AddFilter("System", LogLevel.Warning);
                        })
                        .ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            services.AddSingleton<SettingsService>();
            services.AddSingleton<TraceDirectoryService>();

            services.AddSingleton<AppLogService>();
            services.AddSingleton<ILoggerProvider, AppLogLoggerProvider>();

            services.RegisterServices();
            services.RegisterExecutionServices();

            services.AddTransient<IConnectionTypeFactory, BackupConnectionFactory>();

            services.Configure<SettingsOptions>(context.Configuration.GetSection(nameof(SettingsOptions)));

            services.AddTransient<ConnectServerViewModelFactory>();
            services.AddTransient<DatabaseTabViewModelFactory>();
            services.AddTransient<PageTabViewModelFactory>();
            services.AddTransient<IndexTabViewModelFactory>();
            services.AddTransient<ColumnstoreTabViewModelFactory>();
            services.AddTransient<TraceTabViewModelFactory>();
            services.AddTransient<QueryViewModelFactory>();

            services.AddTransient<QueryRunner>();
            services.AddTransient<LogRecordReader>();
            services.AddTransient<EventReader>();

            services.AddSingleton<AppLogViewModel>();
            services.AddSingleton<SettingsViewModel>();

            services.AddTransient<MainViewModel>();

            services.AddTransient<MainWindow>();
        }).Build();

        UnhandledException += App_UnhandledException;
    }

    public static MainWindow? MainWindow { get; private set; }
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

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = Host.Services.GetRequiredService<MainWindow>();

        MainWindow.Activate();

        await MainWindow.InitializeAsync();
    }

    private static void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new ExceptionMessage(e.Exception));

        e.Handled = true;
    }
}
