using System.Collections.ObjectModel;
using System.Collections.Specialized;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed class MemoryWatchesViewModel
{
    public ObservableCollection<MemoryWatchRowViewModel> Rows { get; } = [];

    public byte[]? Data { get; private set; }

    private ObservableCollection<HexWatch>? Watches { get; set; }

    private bool IsReplacing { get; set; }

    public void Attach(ObservableCollection<HexWatch>? watches)
    {
        if (ReferenceEquals(Watches, watches))
        {
            return;
        }

        if (Watches is { } previous)
        {
            previous.CollectionChanged -= OnWatchesChanged;
        }

        Watches = watches;

        if (watches is not null)
        {
            watches.CollectionChanged += OnWatchesChanged;
        }

        Sync();
    }

    public void Refresh(byte[]? data)
    {
        Data = data;

        foreach (var row in Rows)
        {
            row.Refresh(data);
        }
    }

    public HexWatch Replace(HexWatch watch, string? dataType)
    {
        var replacement = new HexWatch(watch.Offset, watch.Length, dataType) { Label = watch.Label };

        if (Watches is not { } watches)
        {
            return replacement;
        }

        IsReplacing = true;

        watches.Add(replacement);

        watches.Remove(watch);

        IsReplacing = false;

        return replacement;
    }

    public void Remove(HexWatch watch) => Watches?.Remove(watch);

    private void OnWatchesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!IsReplacing)
        {
            Sync();
        }
    }

    private void Sync()
    {
        Rows.Clear();

        foreach (var watch in Watches ?? [])
        {
            Rows.Add(new MemoryWatchRowViewModel(this, watch, Data));
        }
    }
}
