using System.Text.RegularExpressions;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.CallStack.TimeTravel.Locators;

public static partial class LocatorReads
{
    public const int ReturnRegister = 4;

    public const int ObjectKeyParts = 3;

    public const int MaximumHops = 3;

    private const string Module = "sqlmin";

    private static readonly string[] DecoratedTypes =
        ["VPageId@@", "VAllocationPageId@@", "VHPageHandle@@", "URowId@@", "VColumnStoreObjectKey@@"];

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

            foreach (var read in Reads(function, slot.Index, type, returned: false))
            {
                yield return read;
            }
        }

        if (signature.ReturnType is { } returnType && LocatorType.Of(returnType) is { } returned)
        {
            foreach (var read in Reads(function, ReturnRegister, returned, returned: true))
            {
                yield return read;
            }
        }
    }

    private static IEnumerable<LocatorRead> Reads(ulong function, int register, LocatorType type, bool returned)
    {
        var parts = type.Kind == LocatorKind.ColumnStoreObject ? ObjectKeyParts : 1;

        bool[] ends = returned ? [true] : type.IsInOut ? [false, true] : [type.IsOutput];

        foreach (var atReturn in ends)
        {
            for (var part = 0; part < parts; part++)
            {
                yield return new LocatorRead(function, register, atReturn, type.Kind, type.Offset + part * sizeof(ulong), type.Path);
            }
        }
    }

    internal static List<LocatorRead> Find(DiaResolver resolver, ulong moduleAddress, IEnumerable<uint> functions)
    {
        var reads = new List<LocatorRead>();

        foreach (var rva in functions)
        {
            if (resolver.GetDecoratedName(rva) is not { } decoratedName
                || (!DecoratedTypes.Any(t => decoratedName.Contains(t, StringComparison.Ordinal))
                    && !ReferencedType().IsMatch(decoratedName)))
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

    [GeneratedRegex("[PQA]E[AB](?:[FGHIJKMN]|_[JKN]|W4|VHoBtId@@)")]
    private static partial Regex ReferencedType();
}
