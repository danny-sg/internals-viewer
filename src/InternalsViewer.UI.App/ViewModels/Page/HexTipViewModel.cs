using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Converters.Decoder;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Page;

public sealed partial class HexTipViewModel(string? dataType = null) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    private int _start;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    private int _end;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    private string? _customLabel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPinned))]
    private HexWatch? _watch;

    [ObservableProperty]
    private string _value = string.Empty;

    public HexTipViewModel(HexWatch watch) : this(watch.DataType)
    {
        Start = watch.Offset;

        End = watch.End;

        CustomLabel = watch.Label;

        Watch = watch;
    }

    public string? DataType { get; } = dataType;

    public bool IsValueTip => DataType is not null;

    public bool IsPinned => Watch is not null;

    public ObservableCollection<HexTipValue> Values { get; } = [];

    public string Label => CustomLabel ?? GetDefaultLabel();

    public void Describe(int start, int end, byte[] data)
    {
        Start = start;

        End = end;

        Decode(data);
    }

    public void SetLabel(string text)
    {
        var label = text.Trim();

        CustomLabel = label.Length == 0 || label == GetDefaultLabel() ? null : label;
    }

    public void Decode(byte[] data)
    {
        var results = DataDecoder.Decode(data);

        if (DataType is not null)
        {
            Value = results.Find(r => r.DataType == DataType)?.Value ?? string.Empty;

            return;
        }

        if (HasDataTypes(results))
        {
            for (var i = 0; i < results.Count; i++)
            {
                Values[i].Value = results[i].Value;
            }

            return;
        }

        Values.Clear();

        foreach (var result in results)
        {
            Values.Add(new HexTipValue(result.DataType, result.Value));
        }
    }

    partial void OnCustomLabelChanged(string? value)
    {
        if (Watch is { } watch)
        {
            watch.Label = value;
        }
    }

    private bool HasDataTypes(List<DecodeResult> results)
    {
        if (Values.Count != results.Count)
        {
            return false;
        }

        for (var i = 0; i < results.Count; i++)
        {
            if (Values[i].DataType != results[i].DataType)
            {
                return false;
            }
        }

        return true;
    }

    private string GetDefaultLabel()
    {
        if (IsValueTip)
        {
            return $"0x{Start:X8}";
        }

        return Start == End
            ? $"{Start} (0x{Start:X8})"
            : $"{Start} (0x{Start:X8}) - {End} (0x{End:X8})";
    }
}
