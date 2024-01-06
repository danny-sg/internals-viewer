using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.vNext.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace InternalsViewer.UI.App.vNext.ViewModels;

public partial class DatabaseViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<AllocationLayer> allocationLayers = new();

    [ObservableProperty]
    private AllocationLayer? selectedLayer;

    [ObservableProperty]
    private int size;

    [ObservableProperty]
    private AllocationOverViewModel allocationOver = new();
}
