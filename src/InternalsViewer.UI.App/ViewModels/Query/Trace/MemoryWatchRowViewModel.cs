using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Converters.Decoder;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

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
        var bytes = GetBytes(data);

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

        Value = DataType is { } type ? results.Find(r => r.DataType == type)?.Value ?? string.Empty : GetHexText(bytes);
    }

    public void Clear() => Owner.Remove(Watch);

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

    private byte[]? GetBytes(byte[]? data)
        => data is not null && Watch.Offset >= 0 && Watch.Length > 0 && Watch.Offset + Watch.Length <= data.Length
            ? data[Watch.Offset..(Watch.Offset + Watch.Length)]
            : null;

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
