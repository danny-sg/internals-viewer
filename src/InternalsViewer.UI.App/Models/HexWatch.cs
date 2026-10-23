using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models;

public sealed partial class HexWatch(int offset, int length, string? dataType = null) : ObservableObject
{
    [ObservableProperty]
    private string? _label;

    public int Offset { get; } = offset;

    public int Length { get; } = length;

    public string? DataType { get; } = dataType;

    public int End => Offset + Length - 1;
}
