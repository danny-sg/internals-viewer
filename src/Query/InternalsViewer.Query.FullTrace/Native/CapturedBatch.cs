using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CapturedBatch(ulong Function,
                                              ulong Instance,
                                              ulong Iterator,
                                              ulong Batch,
                                              ulong Consumer,
                                              ulong Id,
                                              uint Call,
                                              uint FirstSelected,
                                              uint SelectedCount,
                                              uint FirstVector,
                                              uint VectorCount,
                                              uint Flags);
