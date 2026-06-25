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
        if (collection.Any(predicate))
        {
            var result = collection.Where(predicate).First();

            collection.Remove(result);
            
            return result;
        }

        return default;
    }
}