using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Hooks;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TraceHookFrame(ulong CallId, ulong StackPointer, ulong InstanceAddress, int CallNodeIndex)
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
