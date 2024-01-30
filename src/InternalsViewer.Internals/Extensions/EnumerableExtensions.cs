using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalsViewer.Internals.Extensions;

public static class EnumerableExtensions
{
    public static void AddIf<T>(this List<T> collection, T item, bool condition)
    {
        if (condition)
        {
            collection.Add(item);
        }
    }
}
