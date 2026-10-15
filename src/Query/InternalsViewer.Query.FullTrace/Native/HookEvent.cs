using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct HookEvent
{
    public const int ArgumentCount = 4;

    public ulong Function;

    public ulong Instance;

    public ulong Id;

    public ulong CallCount;

    public fixed ulong Arguments[ArgumentCount];

    public ulong ReturnValue;

    public ulong StackPointer;

    public ulong ReturnAddress;

    public ulong Sequence;

    public ulong Steps;

    public ulong Instructions;

    public int Node;

    public uint Call;

    public uint Thread;

    public HookEventType Type;

    public uint Hook;

    public uint Reserved;
}
