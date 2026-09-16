using System;
using System.Runtime.InteropServices;
using System.Threading;
using InternalsViewer.Query.Debugging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.WindowsAppRuntime;

namespace InternalsViewer.UI.App;

/// <summary>
/// Entry point, taking the place of the one WinUI generates so the executable can also run as the debugger host
/// </summary>
/// <remarks>
/// Started with <see cref="DebuggerHost.Switch"/> the process never touches XAML or the Windows App SDK runtime: it
/// serves a WinDbg session to the app that started it over its standard streams and exits when told to. The SDK's
/// deployment check, which its auto-initializer would otherwise run before any Main, is therefore made here on the
/// app path only, with the same options and the same exit on failure as the auto-initializer.
/// </remarks>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args is [DebuggerHost.Switch])
        {
            return DebuggerHost.RunAsync(Console.In,
                                         Console.Out,
                                         async (engine, remote, token) => await DebuggerEngineClient.ConnectAsync(remote, engine, token))
                               .GetAwaiter()
                               .GetResult();
        }

        var deployment = DeploymentManager.Initialize(new DeploymentInitializeOptions { OnErrorShowUI = true });

        if (deployment.Status != DeploymentStatus.Ok)
        {
            return deployment.ExtendedError.HResult;
        }

        XamlCheckProcessRequirements();

        WinRT.ComWrappersSupport.InitializeComWrappers();

        Application.Start(parameters =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());

            SynchronizationContext.SetSynchronizationContext(context);

            _ = new App();
        });

        return 0;
    }

    [DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();
}
