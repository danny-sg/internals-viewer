using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Trace;

/// <summary>
/// An observable collection that can be refilled in one go, raising a single reset rather than a change per item
/// </summary>
/// <remarks>
/// A run to the end hands the row panes every row the query returned at once. Adding those one at a time makes a bound grid follow every
/// one of them, which is what freezes the UI on a large trace. The collection instance is kept because replacing it rebinds the grid and
/// rebuilds its columns.
/// </remarks>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void Reset(IReadOnlyList<T> items)
    {
        CheckReentrancy();

        Items.Clear();

        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
