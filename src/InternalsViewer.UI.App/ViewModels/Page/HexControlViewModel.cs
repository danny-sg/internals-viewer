using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Converters.Decoder;

namespace InternalsViewer.UI.App.ViewModels.Page;

[ObservableObject]
public partial class HexControlViewModel
{
    [ObservableProperty]
    private bool isDataTipOpen;

    [ObservableProperty]
    private ObservableCollection<DecodeResult> decodeResults = new();

    [ObservableProperty]
    private int startOffset;

    [ObservableProperty]
    private int endOffset;

    [ObservableProperty]
    private string selectedText = string.Empty;

    [ObservableProperty]
    private string dataTipTitle = string.Empty;

    partial void OnSelectedTextChanged(string value)
    {
        DecodeResults = new ObservableCollection<DecodeResult>(DataDecoder.Decode(value));

        IsDataTipOpen = DecodeResults.Count > 0;
    }

    partial void OnStartOffsetChanged(int value)
    {
        SetDataTipTitle();
    }

    partial void OnEndOffsetChanged(int value)
    {
        SetDataTipTitle();
    }

    private void SetDataTipTitle()
    {
        DataTipTitle = $" {StartOffset} (0x{StartOffset:X8}) - {EndOffset} (0x{EndOffset:X8})";
    }   

    [RelayCommand]
    private void CloseDataTip()
    {
        IsDataTipOpen = false;
    }
}
