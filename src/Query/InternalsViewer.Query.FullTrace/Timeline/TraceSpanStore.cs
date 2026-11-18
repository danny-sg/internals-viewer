namespace InternalsViewer.Query.FullTrace.Timeline;

internal sealed class TraceSpanStore(int count)
{
    public TracePosition[] Froms { get; } = new TracePosition[count];

    public TracePosition[] Tos { get; } = new TracePosition[count];

    public int[] ReplayNodeIndexes { get; } = new int[count];

    public uint[] Calls { get; } = new uint[count];

    public byte[] Flags { get; } = new byte[count];

    public int GetCallIndex(int index) => Calls[index] == uint.MaxValue ? -1 : (int)Calls[index];

    public void Set(int index, TracePosition from, TracePosition to, int replayNodeIndex, uint callIndex, byte flags)
    {
        Froms[index] = from;
        Tos[index] = to;
        ReplayNodeIndexes[index] = replayNodeIndex;
        Calls[index] = callIndex;
        Flags[index] = flags;
    }

    public void Sort(int offset, int count)
    {
        if (IsSorted(offset, count))
        {
            return;
        }

        for (var root = count / 2 - 1; root >= 0; root--)
        {
            SiftDown(offset, root, count);
        }

        for (var end = count - 1; end > 0; end--)
        {
            Swap(offset, offset + end);

            SiftDown(offset, 0, end);
        }
    }

    private bool IsSorted(int offset, int count)
    {
        for (var index = offset + 1; index < offset + count; index++)
        {
            if (Froms[index] < Froms[index - 1])
            {
                return false;
            }
        }

        return true;
    }

    private void SiftDown(int offset, int root, int end)
    {
        while (2 * root + 1 < end)
        {
            var child = 2 * root + 1;

            if (child + 1 < end && Froms[offset + child] < Froms[offset + child + 1])
            {
                child++;
            }

            if (Froms[offset + root] >= Froms[offset + child])
            {
                return;
            }

            Swap(offset + root, offset + child);

            root = child;
        }
    }

    private void Swap(int left, int right)
    {
        (Froms[left], Froms[right]) = (Froms[right], Froms[left]);
        (Tos[left], Tos[right]) = (Tos[right], Tos[left]);
        (ReplayNodeIndexes[left], ReplayNodeIndexes[right]) = (ReplayNodeIndexes[right], ReplayNodeIndexes[left]);
        (Calls[left], Calls[right]) = (Calls[right], Calls[left]);
        (Flags[left], Flags[right]) = (Flags[right], Flags[left]);
    }
}
