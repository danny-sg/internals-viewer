using System.Collections.ObjectModel;
using System.ComponentModel;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels;

public interface IAllocationViewModel : INotifyPropertyChanged
{
    bool IsTooltipEnabled { get; }

    double AllocationMapHeight { get; }

    int ExtentCount { get; }

    ObservableCollection<AllocationLayer> AllocationLayers { get; }

    ObservableCollection<AllocationLayer> SelectedLayers { get; }

    PfsChain PfsChain { get; }

    bool IsPfsVisible { get; }

    long SequenceFrom { get; }

    long SequenceTo { get; }
}
