using System.Collections.ObjectModel;
using System.Collections.Specialized;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Page;

public sealed class HexControlViewModel
{
    public HexControlViewModel()
    {
        Watches.CollectionChanged += OnWatchesChanged;
    }

    public ObservableCollection<HexTipViewModel> Tips { get; } = [];

    public ObservableCollection<HexWatch> Watches { get; private set; } = [];

    public HexTipViewModel? SelectionTip { get; private set; }

    public bool HasPins => Watches.Count > 0;

    private byte[] Data { get; set; } = [];

    private int BaseAddress { get; set; }

    public void SetWatches(ObservableCollection<HexWatch> watches)
    {
        Watches.CollectionChanged -= OnWatchesChanged;

        Watches = watches;

        Attach();
    }

    public void Attach()
    {
        Watches.CollectionChanged -= OnWatchesChanged;

        Watches.CollectionChanged += OnWatchesChanged;

        SyncWatches();
    }

    public void Detach()
    {
        Watches.CollectionChanged -= OnWatchesChanged;

        Dismiss();
    }

    public void SetData(byte[] data, int baseAddress)
    {
        Data = data;

        BaseAddress = baseAddress;

        foreach (var tip in Tips)
        {
            Decode(tip);
        }
    }

    public void Select(int start, int end)
    {
        if (GetWatch(start, end, null) is not null || GetBytes(start, end) is not { } bytes)
        {
            Dismiss();

            return;
        }

        var tip = SelectionTip ?? new HexTipViewModel();

        tip.Describe(start, end, bytes);

        if (tip.Values.Count == 0)
        {
            Dismiss();

            return;
        }

        SetValuePins(tip);

        if (SelectionTip is null)
        {
            SelectionTip = tip;

            Tips.Add(tip);
        }
    }

    public void Dismiss()
    {
        if (SelectionTip is not { } tip)
        {
            return;
        }

        SelectionTip = null;

        Tips.Remove(tip);
    }

    public void Pin(HexTipViewModel tip)
    {
        if (tip.IsPinned)
        {
            return;
        }

        if (ReferenceEquals(tip, SelectionTip))
        {
            SelectionTip = null;
        }

        var watch = new HexWatch(tip.Start, tip.End - tip.Start + 1, tip.DataType) { Label = tip.CustomLabel };

        tip.Watch = watch;

        Watches.Add(watch);
    }

    public void TogglePin(HexTipViewModel tip)
    {
        if (tip.Watch is { } watch)
        {
            Watches.Remove(watch);

            return;
        }

        Pin(tip);
    }

    public void TogglePinnedValue(HexTipViewModel tip, HexTipValue value)
    {
        if (GetWatch(tip.Start, tip.End, value.DataType) is { } watch)
        {
            Watches.Remove(watch);

            return;
        }

        Watches.Add(new HexWatch(tip.Start, tip.End - tip.Start + 1, value.DataType));
    }

    public void ClearPins() => Watches.Clear();

    private void OnWatchesChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncWatches();

    private void SyncWatches()
    {
        for (var i = Tips.Count - 1; i >= 0; i--)
        {
            if (Tips[i].Watch is { } watch && !Watches.Contains(watch))
            {
                Tips.RemoveAt(i);
            }
        }

        foreach (var watch in Watches)
        {
            if (GetTip(watch) is not null)
            {
                continue;
            }

            var tip = new HexTipViewModel(watch);

            Decode(tip);

            Tips.Add(tip);
        }

        if (SelectionTip is { } selection && GetWatch(selection.Start, selection.End, null) is not null)
        {
            Dismiss();
        }

        foreach (var tip in Tips)
        {
            SetValuePins(tip);
        }
    }

    private void Decode(HexTipViewModel tip)
    {
        if (GetBytes(tip.Start, tip.End) is { } bytes)
        {
            tip.Decode(bytes);
        }
    }

    private byte[]? GetBytes(int start, int end)
    {
        var first = start - BaseAddress;

        var last = end - BaseAddress;

        return first >= 0 && first <= last && last < Data.Length ? Data[first..(last + 1)] : null;
    }

    private HexTipViewModel? GetTip(HexWatch watch)
    {
        foreach (var tip in Tips)
        {
            if (ReferenceEquals(tip.Watch, watch))
            {
                return tip;
            }
        }

        return null;
    }

    private HexWatch? GetWatch(int start, int end, string? dataType)
    {
        foreach (var watch in Watches)
        {
            if (watch.Offset == start && watch.End == end && watch.DataType == dataType)
            {
                return watch;
            }
        }

        return null;
    }

    private void SetValuePins(HexTipViewModel tip)
    {
        foreach (var value in tip.Values)
        {
            value.IsPinned = GetWatch(tip.Start, tip.End, value.DataType) is not null;
        }
    }
}
