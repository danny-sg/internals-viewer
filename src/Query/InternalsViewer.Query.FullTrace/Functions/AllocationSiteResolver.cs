using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.Query.FullTrace.Functions;

internal static class AllocationSiteResolver
{
    private const string LineType = "int";

    internal static (int File, int Line)? GetSiteSlots(FunctionSignature signature)
    {
        var slots = ArgumentLayout.LayoutOf(signature).Slots;

        for (var index = 0; index + 1 < slots.Count; index++)
        {
            var file = slots[index];

            var line = slots[index + 1];

            if (TraceArgumentCall.IsCaptured(file)
                && TraceArgumentCall.IsCaptured(line)
                && LocatorType.GetLocatorType(file.Type) is { ValueType: LocatorValueType.Text }
                && line.Type == LineType)
            {
                return (file.Index, line.Index);
            }
        }

        return null;
    }

    internal sealed class Collector(IReadOnlySet<uint> allocators)
    {
        private Dictionary<uint, (int File, int Line)> Sites { get; } = [];

        public void Visit(FunctionSymbol symbol)
        {
            if (!allocators.Contains(symbol.Rva))
            {
                return;
            }

            if (!symbol.IsFirst)
            {
                if (!symbol.HasSameParameters)
                {
                    Sites.Remove(symbol.Rva);
                }

                return;
            }

            if (symbol.Parsed is { } signature && GetSiteSlots(signature) is { } slots)
            {
                Sites[symbol.Rva] = slots;
            }
        }

        public MemoryFunction Apply(MemoryFunction function)
            => Sites.TryGetValue((uint)function.Address, out var slots)
                ? function with { FileSlot = slots.File, LineSlot = slots.Line }
                : function;
    }
}
