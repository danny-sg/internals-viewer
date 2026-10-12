using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed class LocatorTable
{
    private LocatorTable(int[] calls, Locator[] locators)
    {
        Calls = calls;
        Locators = locators;
    }

    public int Count => Calls.Length;

    private int[] Calls { get; }

    private Locator[] Locators { get; }

    public static LocatorTable Build(List<(int Call, Locator Locator)> found)
    {
        found.Sort((left, right) => left.Call != right.Call
                                        ? left.Call.CompareTo(right.Call)
                                        : left.Locator.Register.CompareTo(right.Locator.Register));

        return new LocatorTable([.. found.Select(f => f.Call)], [.. found.Select(f => f.Locator)]);
    }

    public IReadOnlyList<Locator> Of(int call)
    {
        var first = SortedSearch.FirstAtOrAfter(Calls, call);

        var end = SortedSearch.FirstAfter(Calls, call, first);

        return end == first ? [] : Locators[first..end];
    }

    public IEnumerable<(int Call, Locator Locator)> Entries()
    {
        for (var index = 0; index < Locators.Length; index++)
        {
            yield return (Calls[index], Locators[index]);
        }
    }

    public static LocatorTable Read(BinaryReader reader)
    {
        var calls = ExportBinary.ReadArray<int>(reader);

        var locators = new Locator[calls.Length];

        for (var index = 0; index < locators.Length; index++)
        {
            var register = reader.ReadInt32();

            var kind = (LocatorKind)reader.ReadByte();

            var value = reader.ReadUInt64();

            locators[index] = new Locator(register, kind, value, reader.ReadUInt64());
        }

        return new LocatorTable(calls, locators);
    }

    public void Write(BinaryWriter writer)
    {
        ExportBinary.WriteArray(writer, Calls);

        foreach (var locator in Locators)
        {
            writer.Write(locator.Register);

            writer.Write((byte)locator.Kind);

            writer.Write(locator.Value);

            writer.Write(locator.Detail);
        }
    }
}
