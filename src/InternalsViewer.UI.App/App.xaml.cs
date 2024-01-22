using Microsoft.Extensions.DependencyInjection;
using System;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace InternalsViewer.UI.App;

public partial class App
{
    private readonly IServiceProvider serviceProvider;

    public Window? Window { get; private set; }

    public App(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;

        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = (MainWindow)ActivatorUtilities.CreateInstance(serviceProvider, typeof(MainWindow));

        Window = window;
        Window.Activate();

        await window.InitializeAsync();
    }
}
