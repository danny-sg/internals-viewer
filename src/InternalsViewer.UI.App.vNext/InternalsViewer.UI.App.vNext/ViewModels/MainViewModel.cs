using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.vNext.Models;
using InternalsViewer.UI.App.vNext.ViewModels.Allocation;
using InternalsViewer.UI.App.vNext.ViewModels.Tabs;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.UI.App.vNext.ViewModels;

public class MainViewModel : ObservableObject
{
    private IServiceProvider ServiceProvider { get; }

    /// <inheritdoc/>
    public MainViewModel(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public T GetService<T>()
    {
        var service =  ServiceProvider.GetService<T>();

        if(service is null)
        {
            throw new InvalidOperationException($"Service {typeof(T).Name} not found");
        }

        return service;
    }

    public void Calibrate()
    {
        var database = new DatabaseSource(null!);

        var viewModel = new DatabaseViewModel(this, database);  

        viewModel.Name = "Calibration";

        var layers = CalibrationBuilder.GenerateLayers();

        viewModel.Database = database;
        viewModel.Size = 1000;
        viewModel.AllocationLayers = new ObservableCollection<AllocationLayer>(layers);
    } 
}