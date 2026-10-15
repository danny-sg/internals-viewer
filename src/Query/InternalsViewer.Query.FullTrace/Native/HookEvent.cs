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

    public ulong* Callers;

    public uint CallerCount;

    public uint Call;

    public uint Thread;

    public HookEventType Type;
}
