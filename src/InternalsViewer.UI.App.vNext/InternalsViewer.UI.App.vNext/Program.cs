using System;
using System.Runtime.InteropServices;
using InternalsViewer.Internals;
using InternalsViewer.UI.App.vNext.Hosting;
using Microsoft.Extensions.Hosting;

#if !DISABLE_XAML_GENERATED_MAIN
#error "This project only works with custom Main entry point. Must set DISABLE_XAML_GENERATED_MAIN to True."
#endif

namespace InternalsViewer.UI.App.vNext;

public static partial class Program
{
    /// <summary>
    /// Ensures that the process can run XAML, and provides a deterministic error if a
    /// check fails. Otherwise, it quietly does nothing.
    /// </summary>
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

        builder.Services.RegisterServices();

        var host = builder.ConfigureWinUi<App>().Build();

        host.Run();
    }
}