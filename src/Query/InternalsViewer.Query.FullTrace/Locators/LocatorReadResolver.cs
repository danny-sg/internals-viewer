using System.Buffers;
using System.Text.RegularExpressions;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;
using InternalsViewer.Query.FullTrace.Functions;

namespace InternalsViewer.Query.FullTrace.Locators;

public static partial class LocatorReadResolver
{
    private const int StackCandidateParameters = 4;

    private static readonly SearchValues<string> DecoratedTypes =
        SearchValues.Create(["VPageId@@", "VAllocationPageId@@", "VHPageHandle@@", "VPageRef@@", "VBTreeHPage@@", "VIndexPageRef@@",
                             "VHeapPageRef@@", "VTextPageRef@@", "VIAMPageRef@@", "VGAMPageRef@@", "VSGAMPageRef@@", "VPFSPageRef@@",
                             "VPSPageRef@@", "URowId@@", "VColumnStoreObjectKey@@", "ULSN@@", "VLSN@@", "PEBD", "PEB_W", "VCTypeInfo@@",
                             "VCXVariant@@", "UtagDBBINDING@@"],
                            StringComparison.Ordinal);

    internal static IEnumerable<LocatorRead> GetReads(ulong function, FunctionSignature signature)
        => GetReads(function, signature, ArgumentLayout.GetLayout(signature));

    internal static IEnumerable<LocatorRead> GetReads(ulong function, FunctionSignature signature, ArgumentLayout layout)
    {
        var slots = layout.Slots;

        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];

            if (slot.Location is not (ArgumentLocation.Register or ArgumentLocation.Stack)
                || slot.Name == ArgumentLayout.ReturnBuffer
                || LocatorType.GetLocatorType(slot.Type) is not { } type
                || (slot.Location == ArgumentLocation.Stack && type.ValueType == LocatorValueType.Referenced))
            {
                continue;
            }

            var count = type.ValueType == LocatorValueType.Bindings ? BindingArguments.GetCountIndex(slots, index) : -1;

            var countRegister = count < 0 ? -1 : slots[count].Index;

            foreach (var read in CreateReads(function, slot.Index, type, returned: false, countRegister))
            {
                yield return read;
            }
        }

        if (signature.ReturnType is { } returnType && LocatorType.GetLocatorType(returnType) is { } returned)
        {
            foreach (var read in CreateReads(function, ArgumentLayout.ReturnRegister, returned, returned: true))
            {
                yield return read;
            }
        }
    }

    internal static IEnumerable<LocatorRead> GetStackReads(ulong function, FunctionSignature signature)
        => GetStackReads(function, ArgumentLayout.GetLayout(signature));

    internal static IEnumerable<LocatorRead> GetStackReads(ulong function, ArgumentLayout layout)
        => layout.Slots
                 .Where(s => s.Location == ArgumentLocation.Stack)
                 .Select(s => new LocatorRead(function, s.Index, false, LocatorValueType.Argument));

    private static bool ReadsLocators(ReadOnlySpan<char> decoratedName)
        => decoratedName.ContainsAny(DecoratedTypes) || ReferencedType().IsMatch(decoratedName);

    private static IEnumerable<LocatorRead> CreateReads(ulong function,
                                                        int register,
                                                        LocatorType type,
                                                        bool returned,
                                                        int countRegister = -1)
    {
        bool[] ends = returned || type.ValueType == LocatorValueType.Variant ? [true] : type.IsInOut ? [false, true] : [type.IsOutput];

        return ends.Select(e => new LocatorRead(function, register, e, type.ValueType, type.Offset, type.Path, countRegister));
    }

    [GeneratedRegex("[PQA]E[AB](?:[FGHIJKMN]|_[JKN]|W4|VHoBtId@@)")]
    private static partial Regex ReferencedType();

    internal sealed class Collector
    {
        private Dictionary<uint, LocatorRead[]> Reads { get; } = [];

        private List<LocatorRead> StackReads { get; } = [];

        public void Visit(FunctionSymbol symbol)
        {
            if (!symbol.IsFirst)
            {
                if (!symbol.HasSameParameters)
                {
                    Reads.Remove(symbol.Rva);
                }

                return;
            }

            if (FunctionSignature.CountParameters(symbol.Signature) is >= StackCandidateParameters && symbol.Layout is { } stacked)
            {
                StackReads.AddRange(GetStackReads(symbol.Rva, stacked));
            }

            if (ReadsLocators(symbol.DecoratedName) && symbol.Parsed is { } signature && symbol.Layout is { } layout)
            {
                Reads[symbol.Rva] = [.. GetReads(symbol.Rva, signature, layout)];
            }
        }

        public LocatorRead[] Build() => [.. Reads.Values.SelectMany(r => r), .. StackReads];
    }
}
