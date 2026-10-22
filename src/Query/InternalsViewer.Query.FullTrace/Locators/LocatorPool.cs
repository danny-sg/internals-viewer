using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed class LocatorPool
{
    private Dictionary<Locator, int> Indexes { get; } = [];

    private ChunkedList<Locator> Locators { get; } = [];

    public int Count => Locators.Count;

    public ref readonly Locator this[int index] => ref Locators[index];

    public int Intern(Locator locator)
    {
        ref var index = ref CollectionsMarshal.GetValueRefOrAddDefault(Indexes, locator, out var exists);

        if (!exists)
        {
            index = Locators.Count;

            Locators.Add(locator);
        }

        return index;
    }

    public (Locator[] Locators, int[] Remap) Compact(bool[] used)
    {
        var remap = new int[Locators.Count];

        var count = 0;

        for (var index = 0; index < remap.Length; index++)
        {
            if (used[index])
            {
                remap[index] = count;

                count++;
            }
        }

        var locators = new Locator[count];

        for (var index = 0; index < remap.Length; index++)
        {
            if (used[index])
            {
                locators[remap[index]] = Locators[index];
            }
        }

        return (locators, remap);
    }
}
