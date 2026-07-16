using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Allocation.Enums;

namespace InternalsViewer.UI.App.ViewModels.Allocation;

public partial class AllocationOverViewModel : ObservableObject
{
    [ObservableProperty]
    private int _pageId;

    [ObservableProperty]
    private int _extentId;

    [ObservableProperty]
    private Color _layerColour = Color.Transparent;

    [ObservableProperty]
    private string _layerName = string.Empty;

    [ObservableProperty]
    private PfsByte _pfsValue = PfsByte.Unknown;
}
