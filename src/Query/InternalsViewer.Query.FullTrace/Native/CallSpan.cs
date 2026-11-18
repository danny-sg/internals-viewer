using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CallSpan(ulong FromSequence,
                                         ulong FromSteps,
                                         ulong ToSequence,
                                         ulong ToSteps,
                                         ulong FromInstructions,
                                         ulong ToInstructions,
                                         int ReplayNodeIndex,
                                         uint Thread,
                                         uint CallIndex,
                                         uint Flags);
