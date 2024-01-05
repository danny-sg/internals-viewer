using Microsoft.Extensions.DependencyInjection;
using System;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace InternalsViewer.UI.App.vNext;

public partial class App
{
    private readonly IServiceProvider serviceProvider;
    private Window? window;

    public App(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = (Window)ActivatorUtilities.CreateInstance(serviceProvider, typeof(MainWindow));
        window.Activate();
    }
}
