using System;
using System.Runtime.InteropServices;
using InternalsViewer.Internals;
using InternalsViewer.UI.App.Helpers.Hosting;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Connections;
using InternalsViewer.UI.App.ViewModels.Database;
using InternalsViewer.UI.App.ViewModels.Page;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

#if !DISABLE_XAML_GENERATED_MAIN
#error "This project only works with custom Main entry point. Must set DISABLE_XAML_GENERATED_MAIN to True."
#endif

namespace InternalsViewer.UI.App;

public static partial class Program
{
    [LibraryImport("Microsoft.ui.xaml.dll")]
    private static partial void XamlCheckProcessRequirements();

    [STAThread]
    private static void Main(string[] args)
    {
        XamlCheckProcessRequirements();

        var builder = Host.CreateApplicationBuilder(args);

        ((IHostApplicationBuilder)builder).Properties.Add(
            HostingExtensions.HostingContextKey,
            new HostingContext { IsLifetimeLinked = true });

        builder.Services.AddSingleton<SettingsService>();

        builder.Services.RegisterServices();

        builder.Services.Configure<SettingsOptions>(builder.Configuration.GetSection(nameof(SettingsOptions)));

        builder.Services.AddTransient<ConnectServerViewModelFactory>();
        builder.Services.AddTransient<DatabaseTabViewModelFactory>();
        builder.Services.AddTransient<PageTabViewModelFactory>();
        builder.Services.AddTransient<MainViewModel>();

        var host = builder.ConfigureWinUi<App>().Build();

        host.Run();
    }
}