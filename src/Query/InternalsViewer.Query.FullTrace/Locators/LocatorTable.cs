using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed partial class LocatorTable
{
    internal LocatorTable(BitPackedArray callIndexes, int[] palette, BitPackedArray paletteIndexes, Locator[] locators)
    {
        CallIndexes = callIndexes;
        Palette = palette;
        PaletteIndexes = paletteIndexes;
        Locators = locators;
    }

    public int Count => CallIndexes.Count;

    public long Bytes => CallIndexes.Bytes + Palette.Length * sizeof(int) + PaletteIndexes.Bytes;

    private BitPackedArray CallIndexes { get; }

    private int[] Palette { get; }

    private BitPackedArray PaletteIndexes { get; }

    private Locator[] Locators { get; }

    public static (BitPackedArray CallIndexes, int[] Palette, BitPackedArray PaletteIndexes) Pack(Span<FoundLocator> found,
                                                                                                  LocatorPool pool,
                                                                                                  Span<int> scratch)
    {
        found.Sort((left, right) => Compare(left, right, pool));

        var selected = found[..SelectPreferred(found, pool)];

        var values = scratch[..selected.Length];

        for (var index = 0; index < selected.Length; index++)
        {
            values[index] = selected[index].CallIndex;
        }

        var callIndexes = BitPackedArray.Pack(values);

        for (var index = 0; index < selected.Length; index++)
        {
            values[index] = selected[index].PoolIndex;
        }

        values.Sort();

        var palette = values[..Unique(values)].ToArray();

        for (var index = 0; index < selected.Length; index++)
        {
            values[index] = Array.BinarySearch(palette, selected[index].PoolIndex);
        }

        return (callIndexes, palette, BitPackedArray.Pack(values));
    }

    public IReadOnlyList<Locator> GetLocators(int callIndex)
    {
        var first = CallIndexes.GetFirstAtOrAfter(callIndex);

        var end = CallIndexes.GetFirstAfter(callIndex, first);

        if (end == first)
        {
            return [];
        }

        var locators = new Locator[end - first];

        for (var index = 0; index < locators.Length; index++)
        {
            locators[index] = GetLocator(first + index);
        }

        return locators;
    }

    private static int Compare(FoundLocator left, FoundLocator right, LocatorPool pool)
    {
        if (left.CallIndex != right.CallIndex)
        {
            return left.CallIndex.CompareTo(right.CallIndex);
        }

        ref readonly var leftLocator = ref pool[left.PoolIndex];

        ref readonly var rightLocator = ref pool[right.PoolIndex];

        if (leftLocator.Register != rightLocator.Register)
        {
            return leftLocator.Register.CompareTo(rightLocator.Register);
        }

        if (leftLocator.ValueType != rightLocator.ValueType)
        {
            return leftLocator.ValueType.CompareTo(rightLocator.ValueType);
        }

        return right.IsReadAtReturn.CompareTo(left.IsReadAtReturn);
    }

    private static int SelectPreferred(Span<FoundLocator> found, LocatorPool pool)
    {
        var count = 0;

        var isGroupReadAtReturn = false;

        for (var index = 0; index < found.Length; index++)
        {
            var isReadAtReturn = found[index].IsReadAtReturn;

            if (index == 0 || !IsSameGroup(found[index - 1], found[index], pool))
            {
                isGroupReadAtReturn = isReadAtReturn;
            }

            if (isGroupReadAtReturn && !isReadAtReturn)
            {
                continue;
            }

            found[count] = found[index];

            count++;
        }

        return count;
    }

    private static bool IsSameGroup(FoundLocator left, FoundLocator right, LocatorPool pool)
        => left.CallIndex == right.CallIndex
           && pool[left.PoolIndex].Register == pool[right.PoolIndex].Register
           && pool[left.PoolIndex].ValueType == pool[right.PoolIndex].ValueType;

    private static int Unique(Span<int> sorted)
    {
        var count = 0;

        for (var index = 0; index < sorted.Length; index++)
        {
            if (count == 0 || sorted[index] != sorted[count - 1])
            {
                sorted[count] = sorted[index];

                count++;
            }
        }

        return count;
    }

    public IEnumerable<(int CallIndex, Locator Locator)> Entries()
    {
        for (var index = 0; index < CallIndexes.Count; index++)
        {
            yield return (CallIndexes[index], GetLocator(index));
        }
    }

    private Locator GetLocator(int index) => Locators[Palette[PaletteIndexes[index]]];
}
