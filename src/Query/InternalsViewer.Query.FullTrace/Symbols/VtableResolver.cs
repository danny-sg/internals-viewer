using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace.Symbols;

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

    private const string BaseStart = "{for `";

    private const string BaseEnd = "'}";

    private const string DecoratedPrefix = "??_7";

    private const string DecoratedBaseMarker = "@@6B";

    private const string DecoratedScopeEnd = "@@";

    private const char DecoratedSeparator = '@';

    private const char DecoratedSpecial = '?';

    private const string ScopeSeparator = "::";

    internal static string? GetClassName(ReadOnlySpan<char> name)
    {
        var end = name.IndexOf(VtableName, StringComparison.Ordinal);

        return end > 0 ? name[..end].ToString() : null;
    }

    internal static string? GetBaseName(ReadOnlySpan<char> name, ReadOnlySpan<char> decoratedName)
    {
        if (name.IndexOf(BaseStart, StringComparison.Ordinal) is >= 0 and var start)
        {
            var named = name[(start + BaseStart.Length)..];

            return named.IndexOf(BaseEnd, StringComparison.Ordinal) is > 0 and var end ? named[..end].ToString() : null;
        }

        if (!decoratedName.StartsWith(DecoratedPrefix, StringComparison.Ordinal)
            || decoratedName.IndexOf(DecoratedBaseMarker, StringComparison.Ordinal) is not (>= 0 and var marker))
        {
            return null;
        }

        var scope = decoratedName[(marker + DecoratedBaseMarker.Length)..];

        if (scope.IsEmpty
            || scope[0] == DecoratedSeparator
            || scope.Contains(DecoratedSpecial)
            || scope.IndexOf(DecoratedScopeEnd, StringComparison.Ordinal) is not (> 0 and var scopeEnd))
        {
            return null;
        }

        var parts = scope[..scopeEnd].ToString().Split(DecoratedSeparator);

        Array.Reverse(parts);

        return string.Join(ScopeSeparator, parts);
    }

    internal static AddressTable<VtableSymbol[]> Read(SymbolTable table)
    {
        var collector = new Collector();

        var symbol = table.Read();

        while (symbol.MoveNext())
        {
            if (!symbol.IsFunction)
            {
                collector.VisitData(symbol.Rva, symbol.Name, symbol.DecoratedName);
            }
        }

        return collector.Build();
    }

    internal static Dictionary<ulong, string> GetUniqueClasses(IReadOnlyDictionary<ulong, VtableSymbol[]> vtables)
    {
        var classes = new Dictionary<ulong, string>();

        foreach (var (address, symbols) in vtables)
        {
            var className = symbols[0].ClassName;

            if (Array.TrueForAll(symbols, s => s.ClassName == className))
            {
                classes[address] = className;
            }
        }

        return classes;
    }

    internal static AddressTable<T> GetFirstMatches<T>(IReadOnlyDictionary<ulong, VtableSymbol[]> vtables, Func<string, T?> match)
        where T : struct
    {
        var matches = new Dictionary<ulong, T>();

        foreach (var (address, symbols) in vtables)
        {
            foreach (var symbol in symbols)
            {
                if (match(symbol.ClassName) is { } value)
                {
                    matches[address] = value;

                    break;
                }
            }
        }

        return new(matches);
    }

    internal sealed class Collector
    {
        private Dictionary<uint, List<VtableSymbol>> Symbols { get; } = [];

        public void VisitData(uint rva, ReadOnlySpan<char> name, ReadOnlySpan<char> decoratedName = default)
        {
            if (GetClassName(name) is not { } className)
            {
                return;
            }

            ref var symbols = ref CollectionsMarshal.GetValueRefOrAddDefault(Symbols, rva, out _);

            symbols ??= [];

            var symbol = new VtableSymbol(className, GetBaseName(name, decoratedName));

            if (!symbols.Contains(symbol))
            {
                symbols.Add(symbol);
            }
        }

        public AddressTable<VtableSymbol[]> Build() => new(Symbols.ToDictionary(c => (ulong)c.Key, c => c.Value.ToArray()));
    }
}
