using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CallSpan(ulong StartSequence,
                                         ulong StartSteps,
                                         ulong EndSequence,
                                         ulong EndSteps,
                                         ulong StartInstructions,
                                         ulong EndInstructions,
                                         int Node,
                                         uint Thread,
                                         uint Call,
                                         uint Flags);
