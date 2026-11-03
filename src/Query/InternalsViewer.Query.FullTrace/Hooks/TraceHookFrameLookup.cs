namespace InternalsViewer.Query.FullTrace.Hooks;

internal static class TraceHookFrameLookup
{
    public static int GetOwnerIndex(ReadOnlySpan<TraceHookFrame> frames, ulong address)
    {
        for (var index = 0; index < frames.Length; index++)
        {
            if (frames[index].StackPointer > address)
            {
                return index;
            }
        }

        return -1;
    }
}
