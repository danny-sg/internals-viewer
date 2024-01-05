// Distributed under the MIT License. See accompanying file LICENSE or copy
// at https://opensource.org/licenses/MIT).
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using InternalsViewer.UI.App.vNext.Hosting;
using Microsoft.Extensions.Hosting;
#if !DISABLE_XAML_GENERATED_MAIN
#error "This project only works with custom Main entry point. Must set DISABLE_XAML_GENERATED_MAIN to True."
#endif

namespace InternalsViewer.UI.App.vNext;

/// <summary>
/// The Main entry of the application.
/// <para>
/// Overrides the usual WinUI XAML entry point in order to be able to control
/// what exactly happens at the entry point of the application. Customized here
/// to build an application <see cref="Host" /> and populate it with the
/// default services (such as Configuration, Logging, etc...) and a specialized
/// <see cref="IHostedService" /> for running the User Interface thread.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// A convenience extension method <see cref="HostingExtensions.ConfigureWinUi{TApplication}" /> is provided to
/// simplify the setup of the User Interface hosted service for WinUI
/// applications.
/// </para>
/// <para>
/// The WinUI service configuration supports customization, through a
/// <see cref="HostingContext" /> object placed in the
/// <see cref="IHostApplicationBuilder.Properties" /> of the host builder.
/// Currently the <see cref="BaseHostingContext.IsLifetimeLinked" /> property
/// allows to specify if the User Interface thread lifetime is linked to the
/// application lifetime or not. When the two lifetimes are linked, terminating
/// either of them will resulting in terminating the other.
/// </para>
/// </remarks>
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

        // Use a default application host builder, which comes with logging,
        // configuration providers for environment variables, command line,
        // 'appsettings.json' and secrets.
        var builder = Host.CreateApplicationBuilder(args);

        // You can further customize and enhance the builder with additional
        // configuration sources, logging providers, etc.

        // Setup and provision the hosting context for the User Interface
        // service.
        ((IHostApplicationBuilder)builder).Properties.Add(
            HostingExtensions.HostingContextKey,
            new HostingContext() { IsLifetimeLinked = true });

        // Add the WinUI User Interface hosted service as early as possible to
        // allow the UI to start showing up while you continue setting up other
        // services not required for the UI.
        var host = builder.ConfigureWinUi<App>()
            .Build();

        // Finally start the host. This will block until the application
        // lifetime is terminated through CTRL+C, closing the UI windows or
        // programmatically.
        host.Run();
    }
}