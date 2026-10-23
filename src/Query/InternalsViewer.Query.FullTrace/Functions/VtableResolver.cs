using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Functions;

internal static class VtableResolver
{
    private const string VtableName = "::`vftable'";

    internal static string? GetClassName(ReadOnlySpan<char> name)
    {
        var end = name.IndexOf(VtableName, StringComparison.Ordinal);

        return end > 0 ? name[..end].ToString() : null;
    }

    internal static Dictionary<ulong, string> GetUniqueClasses(IReadOnlyDictionary<ulong, string[]> vtables)
        => vtables.Where(v => v.Value.Length == 1).ToDictionary(v => v.Key, v => v.Value[0]);

    internal static Dictionary<ulong, T> GetFirstMatches<T>(IReadOnlyDictionary<ulong, string[]> vtables, Func<string, T?> match)
        where T : struct
    {
        var matches = new Dictionary<ulong, T>();

        foreach (var (address, classes) in vtables)
        {
            foreach (var className in classes)
            {
                if (match(className) is { } value)
                {
                    matches[address] = value;

                    break;
                }
            }
        }

        return matches;
    }

    internal sealed class Collector
    {
        private Dictionary<uint, List<string>> Classes { get; } = [];

        public void VisitData(uint rva, ReadOnlySpan<char> name)
        {
            if (GetClassName(name) is not { } className)
            {
                return;
            }

            ref var classes = ref CollectionsMarshal.GetValueRefOrAddDefault(Classes, rva, out _);

            classes ??= [];

            if (!classes.Contains(className))
            {
                classes.Add(className);
            }
        }

        public Dictionary<ulong, string[]> Build() => Classes.ToDictionary(c => (ulong)c.Key, c => c.Value.ToArray());
    }
}
