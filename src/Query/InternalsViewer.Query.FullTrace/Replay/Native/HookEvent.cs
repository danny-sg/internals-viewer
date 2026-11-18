using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Replay.Native.Enums;

namespace InternalsViewer.Query.FullTrace.Replay.Native;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct HookEvent
{
    public const int ArgumentCount = 4;

    public ulong FunctionAddress;

    public ulong InstanceAddress;

    public ulong CallId;

    public fixed ulong Arguments[ArgumentCount];

    public ulong ReturnValue;

    public ulong StackPointer;

    public ulong ReturnAddress;

    public ulong Sequence;

    public ulong Steps;

    public ulong Instructions;

    public int ReplayNodeIndex;

    public uint CallIndex;

    public uint Thread;

    public HookEventType Type;

    public uint HookMask;
}
