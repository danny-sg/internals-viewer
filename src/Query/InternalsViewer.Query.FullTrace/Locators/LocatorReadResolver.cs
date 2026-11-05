using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.CallStack.Symbols;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;
using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace.Locators;

public static class LocatorReadResolver
{
    private const int StackCandidateParameters = 4;

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

            var count = type.ValueType == LocatorValueType.Bindings ? BindingArgumentLayout.GetCountIndex(slots, index) : -1;

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

    internal static int GetStackSlotCount(ArgumentLayout layout)
    {
        var count = 0;

        for (var index = 0; index < layout.Slots.Count; index++)
        {
            if (layout.Slots[index].Location == ArgumentLocation.Stack)
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasLocatorTypes(FunctionSignature signature)
    {
        for (var index = 0; index < signature.Parameters.Count; index++)
        {
            if (LocatorType.GetLocatorType(signature.Parameters[index]) is not null)
            {
                return true;
            }
        }

        return signature.ReturnType is { } returnType && LocatorType.GetLocatorType(returnType) is not null;
    }

    private static IEnumerable<LocatorRead> CreateReads(ulong function,
                                                        int register,
                                                        LocatorType type,
                                                        bool returned,
                                                        int countRegister = -1)
    {
        bool[] ends = returned || type.ValueType == LocatorValueType.Variant ? [true] : type.IsInOut ? [false, true] : [type.IsOutput];

        return ends.Select(e => new LocatorRead(function, register, e, type.ValueType, type.Offset, type.Path, countRegister));
    }

    internal sealed class Collector
    {
        private Dictionary<uint, LocatorRead[]> Reads { get; } = [];

        private Dictionary<ulong, int> StackSlots { get; } = [];

        public void Visit(SymbolTable.Reader symbol, ArgumentLayout? layout)
        {
            if (!symbol.IsFirst)
            {
                if (!symbol.HasSameParameters)
                {
                    Reads.Remove(symbol.Rva);

                    StackSlots.Remove(symbol.Rva);
                }

                return;
            }

            if (layout is null || symbol.Parsed is not { } signature)
            {
                return;
            }

            if (symbol.ParameterCount is >= StackCandidateParameters && GetStackSlotCount(layout) is > 0 and var count)
            {
                StackSlots[symbol.Rva] = count;
            }

            if (HasLocatorTypes(signature))
            {
                Reads[symbol.Rva] = [.. GetReads(symbol.Rva, signature, layout)];
            }
        }

        public LocatorRead[] Build()
        {
            var reads = new LocatorRead[Reads.Values.Sum(r => r.Length)];

            var at = 0;

            foreach (var function in Reads.Values)
            {
                function.CopyTo(reads, at);

                at += function.Length;
            }

            return reads;
        }

        public AddressTable<int> BuildStackArguments() => new(StackSlots);
    }
}
