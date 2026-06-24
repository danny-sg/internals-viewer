using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

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

public static class DatabaseSourceExtensions
{
    public static AllocationUnit? FindPageAllocationUnit(this DatabaseSource databaseSource, PageAddress page)
    {
        var extent = page.PageId / 8;

        return databaseSource.AllocationUnits
                             .FirstOrDefault(u =>
                                 u.IamChain.IsExtentAllocated(extent, page.FileId, false) ||
                                 u.IamChain.SinglePageSlots.Contains(page)
                                 || u.FirstPage == page
                                 || u.FirstIamPage == page
                                 || u.RootPage == page);
    }
}