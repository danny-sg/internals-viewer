using System.Buffers;
using System.Text.RegularExpressions;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Locators;

public static partial class LocatorReadResolver
{
    public const int ReturnRegister = 8;

    public const int ObjectKeyParts = 3;

    public const int LogSequenceNumberParts = 2;

    public const int MaximumHops = 3;

    private const int StackCandidateParameters = 4;

    private static readonly SearchValues<string> DecoratedTypes =
        SearchValues.Create(["VPageId@@", "VAllocationPageId@@", "VHPageHandle@@", "URowId@@", "VColumnStoreObjectKey@@",
                             "ULSN@@", "VLSN@@", "PEBD", "PEB_W"],
                            StringComparison.Ordinal);

    internal static IEnumerable<LocatorRead> ReadsOf(ulong function, FunctionSignature signature)
    {
        foreach (var slot in ArgumentLayout.LayoutOf(signature).Slots)
        {
            if (slot.Location is not (ArgumentLocation.Register or ArgumentLocation.Stack)
                || slot.Name == ArgumentLayout.ReturnBuffer
                || LocatorType.LocatorTypeOf(slot.Type) is not { } type
                || (slot.Location == ArgumentLocation.Stack && type.Kind == LocatorKind.Referenced))
            {
                continue;
            }

            foreach (var read in GetReads(function, slot.Index, type, returned: false))
            {
                yield return read;
            }
        }

        if (signature.ReturnType is { } returnType && LocatorType.LocatorTypeOf(returnType) is { } returned)
        {
            foreach (var read in GetReads(function, ReturnRegister, returned, returned: true))
            {
                yield return read;
            }
        }
    }

    public static int GetParts(LocatorKind kind) => kind switch
    {
        LocatorKind.ColumnStoreObject => ObjectKeyParts,
        LocatorKind.LogSequenceNumber => LogSequenceNumberParts,
        _ => 1
    };

    internal static IEnumerable<LocatorRead> GetStackReads(ulong function, FunctionSignature signature)
        => ArgumentLayout.LayoutOf(signature)
                         .Slots
                         .Where(s => s.Location == ArgumentLocation.Stack)
                         .Select(s => new LocatorRead(function, s.Index, false, LocatorKind.Argument));

    internal static List<LocatorRead> FindStackReads(DiaResolver resolver, IEnumerable<uint> functions)
    {
        var kept = functions.ToHashSet();

        var reads = new List<LocatorRead>();

        Span<char> buffer = stackalloc char[DiaResolver.MaximumNameLength];

        foreach (var detail in resolver.EnumerateSymbolDetails(string.Empty).Where(d => d.IsFunction && kept.Contains(d.Rva)))
        {
            if (FunctionSignature.ParseParameters(detail.Signature) is not { Count: >= StackCandidateParameters }
                || !kept.Remove(detail.Rva)
                || !resolver.TryGetDecoratedName(detail.Rva, buffer, out var decoratedName, detail.Name)
                || FunctionSignature.Parse(detail.Signature, new string(decoratedName)) is not { } signature)
            {
                continue;
            }

            reads.AddRange(GetStackReads(detail.Rva, signature));
        }

        return reads;
    }

    private static IEnumerable<LocatorRead> GetReads(ulong function, int register, LocatorType type, bool returned)
    {
        var parts = GetParts(type.Kind);

        bool[] ends = returned ? [true] : type.IsInOut ? [false, true] : [type.IsOutput];

        foreach (var isReadAtReturn in ends)
        {
            for (var part = 0; part < parts; part++)
            {
                yield return new LocatorRead(function, register, isReadAtReturn, type.Kind, type.Offset + part * sizeof(ulong), type.Path);
            }
        }
    }

    internal static List<LocatorRead> FindReads(DiaResolver resolver, IEnumerable<uint> functions)
    {
        var reads = new List<LocatorRead>();

        Span<char> buffer = stackalloc char[DiaResolver.MaximumNameLength];

        foreach (var rva in functions)
        {
            if (!resolver.TryGetDecoratedName(rva, buffer, out var name)
                || (!name.ContainsAny(DecoratedTypes) && !ReferencedType().IsMatch(name)))
            {
                continue;
            }

            var decoratedName = new string(name);

            var signatures = resolver.SignaturesAt(rva);

            if (!HasOneParameterList(signatures) || FunctionSignature.Parse(signatures[0], decoratedName) is not { } signature)
            {
                continue;
            }

            reads.AddRange(ReadsOf(rva, signature));
        }

        return reads;
    }

    private static bool HasOneParameterList(IReadOnlyList<string> signatures)
        => signatures.Count == 1
           || (signatures.Count > 1
               && signatures.Select(s => string.Join(',', FunctionSignature.ParseParameters(s) ?? [])).Distinct().Count() == 1);

    [GeneratedRegex("[PQA]E[AB](?:[FGHIJKMN]|_[JKN]|W4|VHoBtId@@)")]
    private static partial Regex ReferencedType();
}
