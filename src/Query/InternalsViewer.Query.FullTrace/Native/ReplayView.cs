using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Native;

internal sealed unsafe class ReplayView : ITraceReplay
{
    public IntPtr View { get; set; }

    public IntPtr Session { get; set; }

    private TraceHookFrame[] Frames { get; set; } = new TraceHookFrame[256];

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

    public bool WatchFrame(uint thread, ulong frame) => Session != IntPtr.Zero && ReplayBridge.WatchFrame(Session, thread, frame);

    public void WatchStack(uint thread, ulong stackPointer)
    {
        if (Session != IntPtr.Zero)
        {
            ReplayBridge.WatchStack(Session, thread, stackPointer);
        }
    }

    public ReadOnlySpan<TraceHookFrame> GetFrames(uint thread)
    {
        if (Session == IntPtr.Zero)
        {
            return [];
        }

        var count = Fill(thread);

        if (count > Frames.Length)
        {
            Frames = new TraceHookFrame[count];

            count = Fill(thread);
        }

        return Frames.AsSpan(0, (int)Math.Min(count, (uint)Frames.Length));
    }

    private uint Fill(uint thread)
    {
        fixed (TraceHookFrame* frames = Frames)
        {
            return ReplayBridge.GetHookFrames(Session, thread, frames, (uint)Frames.Length);
        }
    }
}
