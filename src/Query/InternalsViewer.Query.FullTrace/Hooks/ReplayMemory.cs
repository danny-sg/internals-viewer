using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal sealed unsafe class ReplayMemory : ITraceMemory
{
    public IntPtr View { get; set; }

    public int Read(ulong address, Span<byte> destination)
    {
        if (View == IntPtr.Zero || destination.IsEmpty)
        {
            return 0;
        }

        fixed (byte* pointer = destination)
        {
            return (int)ReplayBridge.ReadTraceMemory(View, address, pointer, (ulong)destination.Length);
        }
    }
}
