using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace InternalsViewer.UI.App.vNext.Helpers;

public static class CollectionExtensions
{
    public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new ObservableCollection<T>(source);
    }
}
