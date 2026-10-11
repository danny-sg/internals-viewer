using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.CallStack.TimeTravel.Locators;

public static class LocatorReads
{
    public const int ReturnRegister = 4;

    public const int ObjectKeyParts = 3;

    private const string Module = "sqlmin";

    private static readonly string[] DecoratedTypes = ["VPageId@@", "VAllocationPageId@@", "URowId@@", "VColumnStoreObjectKey@@"];

    public static bool AppliesTo(string module) => string.Equals(module, Module, StringComparison.OrdinalIgnoreCase);

    public static IEnumerable<LocatorRead> For(ulong function, FunctionSignature signature)
    {
        foreach (var slot in ArgumentLayout.For(signature).Slots)
        {
            if (slot.Location != ArgumentLocation.Register
                || slot.Name == ArgumentLayout.ReturnBuffer
                || LocatorType.Of(slot.Type) is not { } type)
            {
                continue;
            }

            foreach (var read in Reads(function, slot.Index, type.IsOutput, type))
            {
                yield return read;
            }
        }

        if (signature.ReturnType is { } returnType && LocatorType.Of(returnType) is { } returned)
        {
            foreach (var read in Reads(function, ReturnRegister, true, returned))
            {
                yield return read;
            }
        }
    }

    private static IEnumerable<LocatorRead> Reads(ulong function, int register, bool atReturn, LocatorType type)
    {
        var parts = type.Kind == LocatorKind.ColumnStoreObject ? ObjectKeyParts : 1;

        for (var part = 0; part < parts; part++)
        {
            yield return new LocatorRead(function, register, atReturn, type.Kind, type.Offset + part * sizeof(ulong));
        }
    }

    internal static List<LocatorRead> Find(DiaResolver resolver, ulong moduleAddress, IEnumerable<uint> functions)
    {
        var reads = new List<LocatorRead>();

        foreach (var rva in functions)
        {
            if (resolver.GetDecoratedName(rva) is not { } decoratedName
                || !DecoratedTypes.Any(t => decoratedName.Contains(t, StringComparison.Ordinal)))
            {
                continue;
            }

            var signatures = resolver.EnumerateSymbolsAtRva(rva)
                                     .Where(s => s.IsFunction && s.Signature.Length > 0)
                                     .Select(s => s.Signature)
                                     .ToList();

            var parameterLists = signatures.Select(s => string.Join(',', FunctionSignature.ParseParameters(s) ?? []))
                                           .Distinct()
                                           .Count();

            if (parameterLists != 1 || FunctionSignature.Parse(signatures[0], decoratedName) is not { } signature)
            {
                continue;
            }

            reads.AddRange(For(moduleAddress + rva, signature));
        }

        return reads;
    }
}
