using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct AddressEntry(ulong Address,
                                             ulong Call,
                                             ulong Owner,
                                             ulong Until,
                                             int Node,
                                             uint Thread,
                                             uint Operation,
                                             uint Reserved = 0);
