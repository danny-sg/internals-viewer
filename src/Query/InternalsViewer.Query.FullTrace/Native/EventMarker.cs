using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct EventMarker(ulong Function,
                                            ulong Buffer,
                                            ulong Sequence,
                                            ulong Steps,
                                            ulong Instructions,
                                            int Node,
                                            uint Thread);
