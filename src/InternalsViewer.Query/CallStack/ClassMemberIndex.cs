using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.CallStack;

/// <summary>
/// The addresses of every class-scoped public symbol in a PDB, keyed by class
/// </summary>
/// <remarks>
/// Built from one walk of the PDB so later lookups do not repeat it. Only the addresses are kept.
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
