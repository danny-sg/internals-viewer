using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Tree;

public sealed partial class CallTreeHighlight : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;
}
