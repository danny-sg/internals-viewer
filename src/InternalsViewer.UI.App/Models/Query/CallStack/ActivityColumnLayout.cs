using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

/// <summary>
/// The width of the activity band column shared by every row of the call stack tree
/// </summary>
public sealed partial class ActivityColumnLayout : ObservableObject
{
    public const double MinimumWidth = 40;

    public const double MaximumWidth = 600;

    [ObservableProperty]
    private double _width = 100;
}
