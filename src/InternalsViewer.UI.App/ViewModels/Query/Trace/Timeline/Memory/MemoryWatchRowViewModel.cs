using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Converters.Decoder;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryWatchRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private List<string> _dataTypes = [];

    [ObservableProperty]
    private string? _dataType;

    public MemoryWatchRowViewModel(MemoryWatchesViewModel owner, HexWatch watch, byte[]? data)
    {
        Owner = owner;

        Watch = watch;

        DataType = watch.DataType;

        Refresh(data);
    }

    public HexWatch Watch { get; private set; }

    public string Range => Watch.Length == 1 ? $"0x{Watch.Offset:X}" : $"0x{Watch.Offset:X} - 0x{Watch.End:X}";

    public string? Label
    {
        get => Watch.Label;
        set
        {
            if (Watch.Label == value)
            {
                return;
            }

            Watch.Label = value;

            OnPropertyChanged();
        }
    }

    private MemoryWatchesViewModel Owner { get; }

    public void Refresh(byte[]? data)
    {
        var bytes = GetBytes(Watch, data);

        if (bytes is null)
        {
            Value = string.Empty;

            return;
        }

        var results = DataDecoder.Decode(bytes);

        List<string> types = [.. results.Select(r => r.DataType)];

        if (!types.SequenceEqual(DataTypes))
        {
            DataTypes = types;
        }

        Value = GetValue(bytes, results, DataType);
    }

    public void Clear() => Owner.Remove(Watch);

    public static string GetValue(HexWatch watch, byte[]? data)
        => GetBytes(watch, data) is { } bytes ? GetValue(bytes, DataDecoder.Decode(bytes), watch.DataType) : string.Empty;

    partial void OnDataTypeChanged(string? value)
    {
        if (value is null)
        {
            if (Watch.DataType is not null)
            {
                DataType = Watch.DataType;
            }

            return;
        }

        if (value == Watch.DataType)
        {
            return;
        }

        Watch = Owner.Replace(Watch, value);

        Refresh(Owner.Data);
    }

    private static byte[]? GetBytes(HexWatch watch, byte[]? data)
        => data is not null && watch.Offset >= 0 && watch.Length > 0 && watch.Offset + watch.Length <= data.Length
            ? data[watch.Offset..(watch.Offset + watch.Length)]
            : null;

    private static string GetValue(byte[] bytes, List<DecodeResult> results, string? dataType)
        => dataType is { } type ? results.Find(r => r.DataType == type)?.Value ?? string.Empty : GetHexText(bytes);

    private static string GetHexText(byte[] bytes)
    {
        var text = new StringBuilder(bytes.Length * 3);

        foreach (var value in bytes)
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append(value.ToString("X2"));
        }

        return text.ToString();
    }
}
