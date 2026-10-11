using System.Runtime.InteropServices;

namespace InternalsViewer.Query.CallStack.TimeTravel.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TimeTravelCallSpan(ulong StartSequence,
                                                   ulong StartSteps,
                                                   ulong EndSequence,
                                                   ulong EndSteps,
                                                   ulong StartInstructions,
                                                   ulong EndInstructions,
                                                   int Node,
                                                   uint Thread,
                                                   uint Call,
                                                   uint Flags);
