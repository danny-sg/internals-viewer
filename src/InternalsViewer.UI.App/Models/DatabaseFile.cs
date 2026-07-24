using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.ViewModels;

namespace InternalsViewer.UI.App.Models;

public sealed partial class DatabaseFile(IAllocationViewModel parent) : ObservableObject
{
    [ObservableProperty]
    private short _fileId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private int _size;

    [ObservableProperty]
    private bool _isHeaderVisible;

    [ObservableProperty]
    private bool _isViewToggleVisible;

    public IAllocationViewModel Parent { get; } = parent;
}