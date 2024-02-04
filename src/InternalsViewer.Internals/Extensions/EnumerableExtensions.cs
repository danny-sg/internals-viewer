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

    public static T? Pop<T>(this List<T> collection, Func<T, bool> predicate)
    {
        if (collection.Count(predicate) > 0)
        {
            var result = collection.Where(predicate).First();

            collection.Remove(result);
            
            return result;
        }

        return default;
    }
}
