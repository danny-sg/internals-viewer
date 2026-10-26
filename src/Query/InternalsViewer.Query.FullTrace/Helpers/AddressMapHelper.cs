namespace InternalsViewer.Query.FullTrace.Helpers;

internal static class AddressMapHelper
{
    public static Dictionary<ulong, T> Merge<T>(IEnumerable<IReadOnlyDictionary<ulong, T>> maps)
    {
        var merged = new Dictionary<ulong, T>();

        foreach (var map in maps)
        {
            foreach (var (address, value) in map)
            {
                merged.TryAdd(address, value);
            }
        }

        return merged;
    }

    public static Dictionary<ulong, T> Rebase<T>(IReadOnlyDictionary<ulong, T> map, ulong address)
        => map.ToDictionary(e => e.Key + address, e => e.Value);
}
