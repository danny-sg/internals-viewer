using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed partial class ValueDisplay : ObservableObject
{
    [ObservableProperty]
    private bool _isDecoded = true;
}
