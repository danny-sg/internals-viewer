using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Allocation.Enums;

namespace InternalsViewer.UI.App.ViewModels.Allocation;

public partial class AllocationOverViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isOpen;

    [ObservableProperty]
    private int pageId;

    [ObservableProperty]
    private int extentId;

    [ObservableProperty]
    private Color layerColour = Color.Transparent;

    [ObservableProperty]
    private string layerName = string.Empty;

    [ObservableProperty]
    private PfsByte pfsValue = PfsByte.Unknown;
}
