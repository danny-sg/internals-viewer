using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct RegisterChange(uint Register, uint Reserved, ulong Low, ulong High);
