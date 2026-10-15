using System.Buffers;
using System.Text.RegularExpressions;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;

namespace InternalsViewer.Query.FullTrace.Locators;

public static partial class LocatorReadResolver
{
    public const int ReturnRegister = 16;

    private const int StackCandidateParameters = 4;

    private static readonly SearchValues<string> DecoratedTypes =
        SearchValues.Create(["VPageId@@", "VAllocationPageId@@", "VHPageHandle@@", "VPageRef@@", "VBTreeHPage@@", "VIndexPageRef@@",
                             "VHeapPageRef@@", "VTextPageRef@@", "VIAMPageRef@@", "VGAMPageRef@@", "VSGAMPageRef@@", "VPFSPageRef@@",
                             "VPSPageRef@@", "URowId@@", "VColumnStoreObjectKey@@", "ULSN@@", "VLSN@@", "PEBD", "PEB_W", "VCTypeInfo@@"],
                            StringComparison.Ordinal);

    internal static IEnumerable<LocatorRead> GetReads(ulong function, FunctionSignature signature)
    {
        foreach (var slot in ArgumentLayout.LayoutOf(signature).Slots)
        {
            if (slot.Location is not (ArgumentLocation.Register or ArgumentLocation.Stack)
                || slot.Name == ArgumentLayout.ReturnBuffer
                || LocatorType.GetLocatorType(slot.Type) is not { } type
                || (slot.Location == ArgumentLocation.Stack && type.ValueType == LocatorValueType.Referenced))
            {
                continue;
            }

            foreach (var read in CreateReads(function, slot.Index, type, returned: false))
            {
                yield return read;
            }
        }

        if (signature.ReturnType is { } returnType && LocatorType.GetLocatorType(returnType) is { } returned)
        {
            foreach (var read in CreateReads(function, ReturnRegister, returned, returned: true))
            {
                yield return read;
            }
        }
    }

    internal static IEnumerable<LocatorRead> GetStackReads(ulong function, FunctionSignature signature)
        => ArgumentLayout.LayoutOf(signature)
                         .Slots
                         .Where(s => s.Location == ArgumentLocation.Stack)
                         .Select(s => new LocatorRead(function, s.Index, false, LocatorValueType.Argument));

    internal static List<LocatorRead> FindStackReads(DiaResolver resolver, IEnumerable<uint> functions)
    {
        var kept = functions.ToHashSet();

        var reads = new List<LocatorRead>();

        Span<char> buffer = stackalloc char[DiaResolver.MaximumNameLength];

        using var symbols = resolver.ReadSymbols();

        while (symbols.MoveNext())
        {
            if (!symbols.IsFunction
                || FunctionSignature.CountParameters(symbols.Signature) is not >= StackCandidateParameters
                || !kept.Remove(symbols.Rva)
                || !resolver.TryGetDecoratedName(symbols.Rva, buffer, out var decoratedName, symbols.Name)
                || FunctionSignature.Parse(symbols.Signature, decoratedName) is not { } signature)
            {
                continue;
            }

            reads.AddRange(GetStackReads(symbols.Rva, signature));
        }

        return reads;
    }

    internal static List<LocatorRead> FindReads(DiaResolver resolver, IEnumerable<uint> functions)
    {
        var reads = new List<LocatorRead>();

        Span<char> nameBuffer = stackalloc char[DiaResolver.MaximumNameLength];

        Span<char> signatureBuffer = stackalloc char[DiaResolver.MaximumNameLength];

        foreach (var rva in functions)
        {
            if (!resolver.TryGetDecoratedName(rva, nameBuffer, out var decoratedName)
                || (!decoratedName.ContainsAny(DecoratedTypes) && !ReferencedType().IsMatch(decoratedName))
                || !TryGetSignature(resolver, rva, signatureBuffer, out var text)
                || FunctionSignature.Parse(text, decoratedName) is not { } signature)
            {
                continue;
            }

            reads.AddRange(GetReads(rva, signature));
        }

        return reads;
    }

    private static IEnumerable<LocatorRead> CreateReads(ulong function, int register, LocatorType type, bool returned)
    {
        bool[] ends = returned ? [true] : type.IsInOut ? [false, true] : [type.IsOutput];

        return ends.Select(e => new LocatorRead(function, register, e, type.ValueType, type.Offset, type.Path));
    }

    private static bool TryGetSignature(DiaResolver resolver, uint rva, Span<char> buffer, out ReadOnlySpan<char> signature)
    {
        signature = default;

        using var symbols = resolver.ReadSymbolsAtRva(rva);

        while (symbols.MoveNext())
        {
            if (!symbols.IsFunction || symbols.Signature.IsEmpty)
            {
                continue;
            }

            if (signature.IsEmpty)
            {
                symbols.Signature.CopyTo(buffer);

                signature = buffer[..symbols.Signature.Length];
            }
            else if (!FunctionSignature.HasSameParameters(signature, symbols.Signature))
            {
                signature = default;

                return false;
            }
        }

        return !signature.IsEmpty;
    }

    [GeneratedRegex("[PQA]E[AB](?:[FGHIJKMN]|_[JKN]|W4|VHoBtId@@)")]
    private static partial Regex ReferencedType();
}
