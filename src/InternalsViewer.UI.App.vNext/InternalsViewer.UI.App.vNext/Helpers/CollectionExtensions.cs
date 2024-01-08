using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
