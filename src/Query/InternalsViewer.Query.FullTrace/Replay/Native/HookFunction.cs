using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Replay.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct HookFunction(ulong Address, uint EventMask, uint HookMask);
