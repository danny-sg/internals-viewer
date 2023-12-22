using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Engine;

public interface IAllocationChain
{
    bool IsExtentAllocated(int extent, short fileId, bool isInverted);

    PageAddress[] SinglePageSlots { get; }

    List<AllocationPage> Pages { get; }
}
