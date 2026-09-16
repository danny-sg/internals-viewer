using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Connections;

public partial class RecentConnection : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _connectionType = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    public bool IsPasswordRequired { get; init; }

    public bool IsServer => ConnectionType == "Server";
}
