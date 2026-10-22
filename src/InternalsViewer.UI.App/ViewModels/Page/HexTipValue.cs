using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.ViewModels.Page;

public sealed partial class HexTipValue(string dataType, string value) : ObservableObject
{
    [ObservableProperty]
    private string _value = value;

    [ObservableProperty]
    private bool _isPinned;

    public string DataType { get; } = dataType;
}
