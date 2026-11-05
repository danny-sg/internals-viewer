using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.CallStack.Symbols;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Functions;

/// <summary>
/// Resolves function arguments to a file/line (allocation site)
/// </summary>
internal static class AllocationSiteResolver
{
    private const string LineType = "int";

    internal static (int File, int Line)? GetSiteSlots(ArgumentLayout layout)
    {
        var slots = layout.Slots;

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

    internal sealed class Collector
    {
        private Dictionary<uint, (int File, int Line)> Sites { get; } = [];

        public void Visit(SymbolTable.Reader symbol, ArgumentLayout? layout)
        {
            if (!symbol.IsFirst)
            {
                if (!symbol.HasSameParameters)
                {
                    Sites.Remove(symbol.Rva);
                }

                return;
            }

            if (layout is not null && GetSiteSlots(layout) is { } slots)
            {
                Sites[symbol.Rva] = slots;
            }
        }

        public AllocationFunction Apply(AllocationFunction function)
            => Sites.TryGetValue((uint)function.Address, out var slots)
                ? function with { FileSlot = slots.File, LineSlot = slots.Line }
                : function;
    }
}
