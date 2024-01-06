using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.vNext.ViewModels;

public partial class AllocationOverViewModel : ObservableObject
{
    [ObservableProperty]
    private int pageId;

    [ObservableProperty]
    private int extentId;

    [ObservableProperty]
    private Color layerColour;

    [ObservableProperty]
    private string layerName = "None";
}
