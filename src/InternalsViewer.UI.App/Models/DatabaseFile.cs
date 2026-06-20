using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.ViewModels;

namespace InternalsViewer.UI.App.Models;

public sealed partial class DatabaseFile(IAllocationViewModel parent) : ObservableObject
{
    [ObservableProperty]
    private short fileId;

    [ObservableProperty]
    private int size;

    public IAllocationViewModel Parent { get; } = parent;
}