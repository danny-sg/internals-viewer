using System.Runtime.InteropServices;

namespace InternalsViewer.Query.CallStack.TimeTravel.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TimeTravelPointerValue(ulong Function,
                                                       ulong Instance,
                                                       ulong Value,
                                                       uint Call,
                                                       int Register,
                                                       uint Offset = 0,
                                                       uint AtReturn = 0);
