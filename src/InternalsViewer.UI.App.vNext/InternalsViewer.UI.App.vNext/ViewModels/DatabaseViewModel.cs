using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.vNext.Models;
using System.Collections.ObjectModel;

namespace InternalsViewer.UI.App.vNext.ViewModels;

public partial class DatabaseViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<AllocationLayer> allocationLayers = new();

    [ObservableProperty]
    private int size;
}
