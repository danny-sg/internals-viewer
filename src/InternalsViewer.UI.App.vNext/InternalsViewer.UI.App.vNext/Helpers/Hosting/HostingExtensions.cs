// Abdessattar Sassi (2023), Generic Host for WinUI applications - https://github.com/abdes/winui-generic-host
//
// Distributed under the MIT License. See accompanying file LICENSE or copy
// at https://opensource.org/licenses/MIT).
// SPDX-License-Identifier: MIT

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Dispatching;
using WinRT;

namespace InternalsViewer.UI.App.vNext.Helpers.Hosting;

/// <summary>
/// Contains helper extensions for <see cref="HostApplicationBuilder" /> to
/// configure the WinUI service hosting.
/// </summary>
public static class HostingExtensions
{
    /// <summary>
    /// The key used to access the <see cref="HostingContext" /> instance in
    /// <see cref="IHostApplicationBuilder.Properties" />.
    /// </summary>
    public const string HostingContextKey = "UserInterfaceHostingContext";

    /// <summary>
    /// Configures the host builder for a Windows UI (WinUI) application.
    /// </summary>
    /// <typeparam name="TApplication">
    /// The concrete type for the <see cref="Application" /> class.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// This method configures the host builder to support a Windows UI (WinUI)
    /// application. It sets up the necessary services, including the hosting
    /// context, user interface thread, and the hosted service for the user
    /// interface.
    /// </para>
    /// <para>
    /// It attempts to find a <see cref="HostingContext" /> instance from the
    /// host builder properties and if not available creates one and adds it as
    /// a singleton service and as an <see cref="BaseHostingContext" /> service
    /// for use by the <see cref="UserInterfaceHostedService" />.
    /// </para>
    /// <para>
    /// Upon successful completion, the dependency injector will be able to
    /// provide the single instance of the application as a <typeparamref name="TApplication" />
    /// and as an <see cref="Application" /> if it is not the same type.
    /// </para>
    /// </remarks>
    /// <param name="hostBuilder">
    /// The host builder to which the WinUI service needs to be added.
    /// </param>
    /// <returns>The host builder for chaining calls.</returns>
    /// <exception cref="ArgumentException">
    /// When the application's type does not extend <see cref="Application" />.
    /// </exception>
    public static HostApplicationBuilder ConfigureWinUi<TApplication>(this HostApplicationBuilder hostBuilder)
        where TApplication : Application
    {
        HostingContext context;
        if (((IHostApplicationBuilder)hostBuilder).Properties.TryGetValue(HostingContextKey, out var contextAsObject))
        {
            context = (HostingContext)contextAsObject;
        }
        else
        {
            context = new HostingContext { IsLifetimeLinked = true };
            ((IHostApplicationBuilder)hostBuilder).Properties[HostingContextKey] = context;
        }

        _ = hostBuilder.Services.AddSingleton(context);

        _ = hostBuilder.Services.AddSingleton<IUserInterfaceThread, UserInterfaceThread>()
            .AddHostedService<UserInterfaceHostedService>();

        _ = hostBuilder.Services.AddSingleton<TApplication>();

        if (typeof(TApplication) != typeof(Application))
        {
            _ = hostBuilder.Services.AddSingleton<Application>(services => services.GetRequiredService<TApplication>());
        }

        return hostBuilder;
    }
}

/// <summary>
/// Encapsulates the information needed to manage the hosting of a WinUI based
/// User Interface service and associated thread.
/// </summary>
public class HostingContext(bool lifetimeLinked = true) : BaseHostingContext(lifetimeLinked)
{
    /// <summary>Gets or sets the WinUI dispatcher queue.</summary>
    /// <value>The WinUI dispatcher queue.</value>
    public DispatcherQueue? Dispatcher { get; set; }

    /// <summary>Gets or sets the WinUI Application instance.</summary>
    /// <value>The WinUI Application instance.</value>
    public Application? Application { get; set; }
}

/// <summary>
/// Represents the minimal information used to manage the hosting of the User
/// Interface service and associated thread.
/// </summary>
/// <remarks>
/// Extend this class to add data, specific to the UI framework (e.g. WinUI).
/// </remarks>
public class BaseHostingContext(bool lifetimeLinked)
{
    /// <summary>
    /// Gets a value indicating whether the UI lifecycle and the Hosted
    /// Application lifecycle are linked or not.
    /// </summary>
    /// <value>
    /// When <c>true</c>, termination of the UI thread leads to termination of
    /// the Hosted Application and vice versa.
    /// </value>
    public bool IsLifetimeLinked { get; init; } = lifetimeLinked;

    /// <summary>
    /// Gets or sets a value indicating whether the UI thread is running or
    /// not.
    /// </summary>
    /// <value>
    /// When <c>true</c>, it indicates that the UI thread has been started and
    /// is actually running (not waiting to start).
    /// </value>
    public bool IsRunning { get; set; }
}

/// <summary>
/// Represents a a user interface thread in a hosted application.
/// </summary>
public interface IUserInterfaceThread
{
    /// <summary>Starts the User Interface thread.</summary>
    /// <remarks>
    /// Note that after calling this method, the thread may not be actually
    /// running. To check if that is the case or not use the <see cref="BaseHostingContext.IsRunning" />.
    /// </remarks>
    void StartUserInterface();

    /// <summary>
    /// Asynchronously request the User Interface thread to stop.
    /// </summary>
    /// <returns>
    /// The asynchronous task on which to wait for completion.
    /// </returns>
    Task StopUserInterfaceAsync();
}

/// <summary>
/// Represents a base class for a user interface thread in a hosted
/// application.
/// </summary>
/// <typeparam name="T">
/// The concrete type of the class extending <see cref="BaseHostingContext" />
/// which will provide the necessary options to setup the User Interface.
/// </typeparam>
public abstract partial class BaseUserInterfaceThread<T> : IDisposable, IUserInterfaceThread
    where T : BaseHostingContext
{
    private readonly IHostApplicationLifetime hostApplicationLifetime;
    private readonly ILogger logger;
    private readonly ManualResetEvent serviceManualResetEvent = new(false);

    // This manual reset event is signaled when the UI thread completes. It is
    // primarily used in testing environment to ensure that the thread execution
    // completes before the test results are verified.
    private readonly ManualResetEvent uiThreadCompletion = new(false);

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseUserInterfaceThread{T}" /> class.
    /// </summary>
    /// <remarks>
    /// This constructor creates a new thread that runs the UI. The thread is
    /// set to be a background thread with a single-threaded apartment state.
    /// The thread will wait for a signal from the <see cref="serviceManualResetEvent" />
    /// before starting the user interface. The constructor also calls the
    /// <see cref="BeforeStart" /> and <see cref="OnCompletion" /> methods to
    /// perform any initialization and cleanup tasks.
    /// </remarks>
    /// <param name="lifetime">
    /// The hosted application lifetime. Used when the hosting context
    /// indicates that that the UI and the hosted application lifetimes are
    /// linked.
    /// </param>
    /// <param name="context">
    /// The UI service hosting context, partially populated with the
    /// configuration options for the UI thread.
    /// </param>
    /// <param name="logger">The logger to be used by this class.</param>
    protected BaseUserInterfaceThread(IHostApplicationLifetime lifetime, T context, ILogger logger)
    {
        hostApplicationLifetime = lifetime;
        HostingContext = context;
        this.logger = logger;

        // Create a thread which runs the UI
        var newUiThread = new Thread(
            () =>
            {
                BeforeStart();
                _ = serviceManualResetEvent.WaitOne(); // wait for the signal to actually start
                HostingContext.IsRunning = true;
                DoStart();
                OnCompletion();
            })
        {
            IsBackground = true,
        };

        // Set the apartment state
        newUiThread.SetApartmentState(ApartmentState.STA);

        // Transition the new UI thread to the RUNNING state. Note that the
        // thread will actually start after the `serviceManualResetEvent` is
        // set.
        newUiThread.Start();
    }

    /// <summary>
    /// Gets the hosting context for the user interface service.
    /// </summary>
    /// <value>
    /// Although never <c>null</c>, the different fields of the hosting context
    /// may or may not contain valid values depending on the current state of
    /// the User Interface thread. Refer to the concrete class documentation.
    /// </value>
    protected T HostingContext { get; }

    /// <summary>
    /// Actually starts the User Interface thread by setting the underlying
    /// <see cref="ManualResetEvent" />.
    /// </summary>
    /// Initially, the User Interface thread is created and transitioned into
    /// the `RUNNING` state, but it is waiting to be explicitly started via the
    /// <see cref="ManualResetEvent" />
    /// so that we can ensure everything
    /// required for the UI is initialized before we start it. The
    /// responsibility for triggering this rests with the User Interface hosted
    /// service.
    public void StartUserInterface() => serviceManualResetEvent.Set();

    /// <inheritdoc />
    public abstract Task StopUserInterfaceAsync();

    /// <summary>
    /// Wait until the created User Interface Thread completes its
    /// execution.
    /// </summary>
    public void AwaitUiThreadCompletion() => uiThreadCompletion.WaitOne();

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        serviceManualResetEvent.Dispose();
    }

    /// <summary>
    /// Called before the UI thread is started to do any
    /// initialization work.
    /// </summary>
    protected abstract void BeforeStart();

    /// <summary>
    /// Do the work needed to actually start the User Interface thread.
    /// </summary>
    protected abstract void DoStart();

    /// <summary>
    /// Called upon completion of the UI thread (i.e. no more UI). Will
    /// eventually request the hosted application to stop depending on whether
    /// the UI lifecycle and the application lifecycle are linked or not.
    /// </summary>
    /// <seealso cref="BaseHostingContext.IsLifetimeLinked" />
    private void OnCompletion()
    {
        Debug.Assert(
            HostingContext.IsRunning,
            "Expecting the `IsRunning` flag to be set when `OnCompletion() is called");
        HostingContext.IsRunning = false;
        if (HostingContext.IsLifetimeLinked)
        {
            StoppingHostApplication();

            if (!hostApplicationLifetime.ApplicationStopped.IsCancellationRequested &&
                !hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                hostApplicationLifetime.StopApplication();
            }
        }

        _ = uiThreadCompletion.Set();
    }

    [LoggerMessage(
        SkipEnabledCheck = true,
        Level = LogLevel.Debug,
        Message = "Stopping hosted application due to user interface thread exit.")]
    partial void StoppingHostApplication();
}

/// <summary>
/// Implementation for a WinUI based UI thread. This is basically a drop-in
/// replacement for the bootstrap code auto-generated by the WinUI XAML in the
/// `Main` entry point.
/// </summary>
/// <param name="serviceProvider">
/// The Dependency Injector's <see cref="IServiceProvider" />.
/// </param>
/// <param name="lifetime">
/// The host application lifetime. Should be provided by the DI injector and is
/// used when the hosting context indicates that that the UI and the host
/// application lifetimes are linked.
/// </param>
/// <param name="context">
/// The UI service hosting context. Should be provided by the DI injector and
/// partially populated with the configuration options for the UI thread.
/// </param>
/// <param name="loggerFactory">
/// Used to obtain a logger for this class. If not possible, a <see cref="NullLogger" /> will be used instead.
/// </param>
public class UserInterfaceThread(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    HostingContext context,
    ILoggerFactory? loggerFactory) : BaseUserInterfaceThread<HostingContext>(
    lifetime,
    context,
    loggerFactory?.CreateLogger<UserInterfaceThread>() ?? MakeNullLogger())
{
    /// <inheritdoc />
    public override Task StopUserInterfaceAsync()
    {
        Debug.Assert(
            HostingContext.Application is not null,
            "Expecting the `Application` in the context to not be null.");

        TaskCompletionSource completion = new();
        _ = HostingContext.Dispatcher!.TryEnqueue(
            () =>
            {
                HostingContext.Application?.Exit();
                completion.SetResult();
            });
        return completion.Task;
    }

    /// <inheritdoc />
    protected override void BeforeStart() => ComWrappersSupport.InitializeComWrappers();

    /// <inheritdoc />
    protected override void DoStart() => Application.Start(
        _ =>
        {
            HostingContext.Dispatcher = DispatcherQueue.GetForCurrentThread();
            DispatcherQueueSynchronizationContext context = new(HostingContext.Dispatcher);
            SynchronizationContext.SetSynchronizationContext(context);

            HostingContext.Application = serviceProvider.GetRequiredService<Application>();

            /*
             * TODO: here we can add code that initializes the UI before the
             * main window is created and activated For example: unhandled
             * exception handlers, maybe instancing, activation, etc...
             */

            // NOTE: First window creation is to be handled in Application.OnLaunched()
        });

    private static ILogger MakeNullLogger() => NullLoggerFactory.Instance.CreateLogger<UserInterfaceThread>();
}

/// <summary>
/// A long running service that will execute the User Interface
/// thread.
/// </summary>
/// <remarks>
/// <para>
/// Should be registered (only once) in the services collection with the
/// <see cref="ServiceCollectionHostedServiceExtensions.AddHostedService{THostedService}(IServiceCollection)">
/// AddHostedService
/// </see>
/// extension method.
/// </para>
/// <para>
/// Expects the <see cref="UserInterfaceThread" /> and <see cref="HostingContext" />
/// singleton instances to be setup in the dependency injector.
/// </para>
/// </remarks>
/// <param name="loggerFactory">
/// We inject a <see cref="ILoggerFactory" /> to be able to silently use a
/// <see cref="NullLogger" /> if we fail to obtain a <see cref="ILogger" />
/// from the Dependency Injector.
/// </param>
/// <param name="uiThread">
/// The <see cref="UserInterfaceThread" />
/// instance.
/// </param>
/// <param name="context">The <see cref="HostingContext" /> instance.</param>
public partial class UserInterfaceHostedService(
    ILoggerFactory? loggerFactory,
    IUserInterfaceThread uiThread,
    HostingContext context) : IHostedService
{
    private readonly ILogger logger = loggerFactory?.CreateLogger<UserInterfaceHostedService>() ??
                                      NullLoggerFactory.Instance.CreateLogger<UserInterfaceHostedService>();

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        // Make the UI thread go
        uiThread.StartUserInterface();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || !context.IsRunning)
        {
            return Task.CompletedTask;
        }

        StoppingUserInterfaceThread();
        return uiThread.StopUserInterfaceAsync();
    }

    [LoggerMessage(
        SkipEnabledCheck = true,
        Level = LogLevel.Debug,
        Message = "Stopping user interface thread due to application exiting.")]
    partial void StoppingUserInterfaceThread();
}