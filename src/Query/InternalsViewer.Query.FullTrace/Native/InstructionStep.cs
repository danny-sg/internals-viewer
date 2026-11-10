using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct InstructionStep(ulong Sequence,
                                                ulong Steps,
                                                ulong ProgramCounter,
                                                uint FirstChange,
                                                uint ChangeCount,
                                                int Depth);
