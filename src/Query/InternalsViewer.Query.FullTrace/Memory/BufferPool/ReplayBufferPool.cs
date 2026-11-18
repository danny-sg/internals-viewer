using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

internal sealed class ReplayBufferPool
{
    internal List<TraceBufferPage> Sightings { get; } = [];

    internal Dictionary<(PageAddress Page, ushort DatabaseId), (ushort[] Offsets, PageType Type)> SlotLayouts { get; } = [];

    public void AddSighting(TraceBufferPage page) => Sightings.Add(page);

    public void AddLayout(PageAddress page, ushort databaseId, ushort[] offsets, PageType type = PageType.None)
        => SlotLayouts[(page, databaseId)] = (offsets, type);
}
