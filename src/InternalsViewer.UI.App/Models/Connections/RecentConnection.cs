using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace InternalsViewer.UI.App.Models.Connections;

public partial class RecentConnection : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string connectionType = string.Empty;

    [ObservableProperty]
    private string value = string.Empty;

    [ObservableProperty]
    private string id = Guid.NewGuid().ToString();

    public bool IsPasswordRequired { get; init; }
}
