using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.CallStack;

/// <summary>
/// The addresses of every class-scoped public symbol in a PDB, keyed by class
/// </summary>
/// <remarks>
/// Built from one walk of the PDB so later lookups do not repeat it. Only the addresses are kept: a class's members are
/// described again from the symbols at those addresses when it is listed, which keeps the index to a few bytes per
/// symbol and stays exact where identical-code folding puts several symbols at one address.
/// </remarks>
internal sealed class ClassMemberIndex
{
    private ClassMemberIndex(Dictionary<string, List<uint>> rvasByClass)
    {
        RvasByClass = rvasByClass;
    }

    private Dictionary<string, List<uint>> RvasByClass { get; }

    public static ClassMemberIndex Build(DiaResolver resolver)
    {
        var rvasByClass = new Dictionary<string, List<uint>>(StringComparer.Ordinal);

        foreach (var symbol in resolver.EnumerateSymbolDetails(string.Empty, includeSignature: false))
        {
            if (symbol.Name.Contains('`'))
            {
                continue;
            }

            var separator = ResolvedCallstackFrameParser.FindClassMethodSeparator(symbol.Name);

            if (separator <= 0)
            {
                continue;
            }

            var className = symbol.Name[..separator];

            if (!rvasByClass.TryGetValue(className, out var rvas))
            {
                rvas = [];

                rvasByClass[className] = rvas;
            }

            rvas.Add(symbol.Rva);
        }

        return new ClassMemberIndex(rvasByClass);
    }

    public IReadOnlyList<uint> Find(string className)
        => RvasByClass.TryGetValue(className, out var rvas) ? rvas : [];
}
