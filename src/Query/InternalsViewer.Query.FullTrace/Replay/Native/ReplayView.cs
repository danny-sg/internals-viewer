using InternalsViewer.Query.FullTrace.Replay.Hooks;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Replay.Hooks.Interfaces;

namespace InternalsViewer.Query.FullTrace.Replay.Native;

internal sealed unsafe class ReplayView : ITraceReplay
{
    public EngineLayout Layout { get; init; } = EngineLayout.Latest;

    private IntPtr View { get; set; }

    private IntPtr Session { get; set; }

    private TraceHookFrame[] Frames { get; set; } = new TraceHookFrame[256];

    private int FrameCount { get; set; } = -1;

    private uint FrameThread { get; set; }

    private ulong[] StackArguments { get; } = new ulong[TraceHookArgumentReader.StackSlotCount];

    private bool IsStackRead { get; set; }

    public void Begin(IntPtr view, IntPtr session)
    {
        View = view;

        Session = session;

        FrameCount = -1;

        IsStackRead = false;
    }

    public void End() => Begin(IntPtr.Zero, IntPtr.Zero);

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

    public ReadOnlySpan<TraceHookFrame> GetFrames(uint thread)
    {
        if (Session == IntPtr.Zero)
        {
            return [];
        }

        if (FrameCount < 0 || FrameThread != thread)
        {
            var count = Fill(thread);

            if (count > Frames.Length)
            {
                Frames = new TraceHookFrame[count];

                count = Fill(thread);
            }

            FrameCount = (int)Math.Min(count, (uint)Frames.Length);

            FrameThread = thread;
        }

        return Frames.AsSpan(0, FrameCount);
    }

    public ReadOnlySpan<ulong> GetStackArguments(in TraceHookCall call)
    {
        if (!IsStackRead)
        {
            TraceHookArgumentReader.ReadStackArguments(this, call, StackArguments);

            IsStackRead = true;
        }

        return StackArguments;
    }

    private uint Fill(uint thread)
    {
        fixed (TraceHookFrame* frames = Frames)
        {
            return ReplayBridge.GetHookFrames(Session, thread, frames, (uint)Frames.Length);
        }
    }
}
