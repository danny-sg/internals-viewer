using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.vNext.ViewModels.Allocation;

public partial class AllocationOverViewModel : ObservableObject
{
    [ObservableProperty]
    private int pageId;

    [ObservableProperty]
    private int extentId;

    [ObservableProperty]
    private Color layerColour = Color.Transparent;

    [ObservableProperty]
    private string layerName = string.Empty;
}
