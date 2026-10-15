using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CapturedVector(uint FirstSlot,
                                               uint SlotCount,
                                               uint FirstDeepValue,
                                               uint DeepValueCount,
                                               ushort VectorId,
                                               ushort Flags,
                                               uint Reserved = 0);
