using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.Extensions;

namespace InternalsViewer.Query.FullTrace.Stubs;

internal sealed class StubMemory((ulong Start, ulong End)[] images)
{
    private Dictionary<ulong, ulong> ImageValues { get; } = [];

    public bool TryRead(ITraceReplay replay, ulong address, out ulong value)
    {
        if (replay.TryRead(address, out value))
        {
            if (IsImage(address))
            {
                ImageValues[address] = value;
            }

            return true;
        }

        return ImageValues.TryGetValue(address, out value);
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
