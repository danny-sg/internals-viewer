using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Converters.Decoder;

namespace InternalsViewer.UI.App.ViewModels.Page;

[ObservableObject]
public partial class HexControlViewModel
{
    [ObservableProperty]
    private bool _isDataTipOpen;

    [ObservableProperty]
    private ObservableCollection<DecodeResult> _decodeResults = [];

    [ObservableProperty]
    private int _startOffset;

    [ObservableProperty]
    private int _endOffset;

    [ObservableProperty]
    private string _selectedText = string.Empty;

    [ObservableProperty]
    private string _dataTipTitle = string.Empty;

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
        if (StartOffset == EndOffset)
        {
            DataTipTitle = $"  {StartOffset - 1} (0x{StartOffset - 1:X8})";
        }
        else
        {
            DataTipTitle = $" {StartOffset - 1} (0x{StartOffset - 1:X8}) - {EndOffset - 1} (0x{EndOffset - 1:X8})";
        }
    }

    [RelayCommand]
    private void CloseDataTip()
    {
        IsDataTipOpen = false;
    }
}
