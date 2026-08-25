using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Allocations;

namespace InternalsViewer.UI.App.ViewModels;

public interface IAllocationViewModel : INotifyPropertyChanged
{
    bool IsTooltipEnabled { get; }

    double AllocationMapHeight { get; }

    int ExtentCount { get; }

    ObservableCollection<AllocationLayer> AllocationLayers { get; }

    IReadOnlyList<AllocationBorder> AllocationBorders { get; }

    PfsChain PfsChain { get; }

    bool IsPfsVisible { get; }

    long PlayheadTimeUs { get; }
}
