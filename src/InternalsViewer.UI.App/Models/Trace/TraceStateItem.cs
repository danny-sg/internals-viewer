using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.App.Models.Trace;

public sealed partial class TraceStateItem(string name) : ObservableObject
{
    public string Name { get; } = name;

    public bool? Flag { get; init; }

    public Visibility TickVisibility => Flag == true ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CrossVisibility => Flag == false ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ValueVisibility => Flag is null ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    private string _value = string.Empty;
}
