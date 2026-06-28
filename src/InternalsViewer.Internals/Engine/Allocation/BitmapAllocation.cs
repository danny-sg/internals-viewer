using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Internals.Engine.Allocation;

public sealed class BitmapAllocation(short fileId, int startOffset, byte[] allocations) : IAllocationChain
{
    public short FileId { get; } = fileId;

    public byte[] Allocations { get; } = allocations;

    public PageAddress[] SinglePageSlots { get; } = [];

    public bool IsExtentAllocated(int extent, short fileId, bool isInverted)
    {
        if (fileId != FileId)
        {
            return isInverted;
        }

        var bitIndex = extent + startOffset;

        return ((Allocations[bitIndex >> 3] >> (bitIndex & 7) & 1) != 0) ^ isInverted;
    }

    public bool AnyExtentsAllocated(int fromExtent, int toExtent, short fileId, bool isInverted)
    {
        if (fileId != FileId)
        {
            return isInverted;
        }

        for (var i = fromExtent; i <= toExtent; i++)
        {
            var bitIndex = i + startOffset;

            if (((Allocations[bitIndex >> 3] >> (bitIndex & 7) & 1) != 0) ^ isInverted)
            {
                return true;
            }
        }

        return false;
    }
}