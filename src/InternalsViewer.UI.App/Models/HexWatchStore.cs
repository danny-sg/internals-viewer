using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace InternalsViewer.UI.App.Models;

public sealed class HexWatchStore
{
    public Dictionary<string, ObservableCollection<HexWatch>> Watches { get; } = [];

    public ObservableCollection<HexWatch> GetWatches(string name)
    {
        if (!Watches.TryGetValue(name, out var watches))
        {
            watches = [];

            Watches[name] = watches;
        }

        return watches;
    }
}
