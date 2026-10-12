using System.Runtime.InteropServices;

namespace InternalsViewer.Query.CallStack.TimeTravel.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TimeTravelMarker(ulong Function,
                                                 ulong Buffer,
                                                 ulong Sequence,
                                                 ulong Steps,
                                                 ulong Instructions,
                                                 int Node,
                                                 uint Thread);
