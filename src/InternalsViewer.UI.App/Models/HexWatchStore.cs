using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace InternalsViewer.UI.App.Models;

public sealed class HexWatchStore
{
    public event EventHandler? Changed;

    public Dictionary<string, ObservableCollection<HexWatch>> Watches { get; } = [];

    public ObservableCollection<HexWatch> GetWatches(string name)
    {
        if (!Watches.TryGetValue(name, out var watches))
        {
            watches = [];

            watches.CollectionChanged += OnWatchesChanged;

            Watches[name] = watches;
        }

        return watches;
    }

    public int RemoveTemporary()
    {
        var removed = 0;

        foreach (var watches in Watches.Values)
        {
            foreach (var watch in watches.Where(w => w.IsTemporary).ToArray())
            {
                watches.Remove(watch);

                removed++;
            }
        }

        return removed;
    }

    public Dictionary<string, HexWatch[]> GetSnapshot()
        => Watches.Where(w => w.Value.Count > 0).ToDictionary(w => w.Key, w => w.Value.ToArray());

    private void OnWatchesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var watch in e.OldItems?.OfType<HexWatch>() ?? [])
        {
            watch.PropertyChanged -= OnWatchChanged;
        }

        foreach (var watch in e.NewItems?.OfType<HexWatch>() ?? [])
        {
            watch.PropertyChanged += OnWatchChanged;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnWatchChanged(object? sender, PropertyChangedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
