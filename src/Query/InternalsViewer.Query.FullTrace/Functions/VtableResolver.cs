using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Functions;

/// <summary>
/// Resolves class names from vtable (virtual function table) symbols
/// </summary>
/// <remarks>
/// A vtable is the table of function pointers the C++ compiler generates for a class with virtual methods. Virtual calls are where the
/// method is chosen at runtime in polymorphic types. Each type's vtable maps the virtual methods to the specific addresses for that type.
/// 
/// Every instance of such a class starts with a hidden pointer to its class's vtable, which is how virtual calls dispatch to the correct
/// override at runtime.
///
/// This makes vtables useful for identification: reading the pointer at the start of an object gives the vtable address, and the PDB
/// contains a data symbol named Class::`vftable' at that address, so a vtable address can be mapped back to a class name without knowing
/// anything else about the object.
///
/// The same vtable address can be shared by more than one class (for example identical vtables folded together by the linker), so the
/// collector keeps every class name seen at an address:
///
///     - GetUniqueClasses - keeps only addresses with a single candidate class, where identification is unambiguous
///
///     - GetFirstMatches - resolves an address to the first candidate class accepted by a caller supplied match
/// </remarks>
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
