using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.Interfaces;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

internal readonly record struct PlanOutputValueSource(PlanOutputLinkType Type, ulong Address, ulong[] Chain, int? Offset)
{
    public ulong? Follow(IMemoryReader memory)
    {
        var address = Address;

        foreach (var offset in Chain)
        {
            if (!memory.TryRead(address + offset, out address) || address < TraceAddress.MinimumAddress)
            {
                return null;
            }
        }

        return address;
    }
}
