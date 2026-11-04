using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.Extensions;

namespace InternalsViewer.Query.FullTrace.Stubs;

/// <summary>
/// Replay memory reads with a fallback cache of values previously read from module image ranges
/// </summary>
internal sealed class StubMemory((ulong Start, ulong End)[] images)
{
    private Dictionary<ulong, ulong> ImageValueCache { get; } = [];

    public bool TryRead(ITraceReplay replay, ulong address, out ulong value)
    {
        if (replay.TryRead(address, out value))
        {
            if (IsImage(address))
            {
                ImageValueCache[address] = value;
            }

            return true;
        }

        return ImageValueCache.TryGetValue(address, out value);
    }

    private bool IsImage(ulong address)
    {
        foreach (var (start, end) in images)
        {
            if (address >= start && address < end)
            {
                return true;
            }
        }

        return false;
    }
}
