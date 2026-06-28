using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Engine;

public interface IAllocationPageChain<T> : IAllocationChain
    where T : AllocationPage
{
    List<T> Pages { get; }
}

public interface IAllocationChain
{
    PageAddress[] SinglePageSlots { get; }

    bool IsExtentAllocated(int extent, short fileId, bool isInverted);

    bool AnyExtentsAllocated(int fromExtent, int toExtent, short fileId, bool isInverted);
}