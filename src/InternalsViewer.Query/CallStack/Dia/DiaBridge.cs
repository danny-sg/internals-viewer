using System.Runtime.InteropServices;
using System.Text;

namespace InternalsViewer.Query.CallStack.Dia;

internal static class DiaBridge
{
    [DllImport("InternalsViewer.Query.DiaBridge.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr OpenPdb(string pdbPath);

    // The native exports return the 1-byte C++ bool, which only guarantees AL on x64 — marshalling as the 4-byte Win32
    // BOOL (UnmanagedType.Bool) reads the undefined upper bytes of EAX, so a returned false can read as true.
    [DllImport("InternalsViewer.Query.DiaBridge.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool ResolveRva(IntPtr session,
                                         uint rva,
                                         StringBuilder buffer,
                                         int bufferLength);

    [DllImport("InternalsViewer.Query.DiaBridge.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr BeginEnumSymbols(IntPtr session, string prefix);

    [DllImport("InternalsViewer.Query.DiaBridge.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool NextSymbol(IntPtr enumerator, StringBuilder buffer, int bufferLength);

    [DllImport("InternalsViewer.Query.DiaBridge.dll")]
    public static extern void EndEnumSymbols(IntPtr enumerator);

    [DllImport("InternalsViewer.Query.DiaBridge.dll")]
    public static extern void ClosePdb(IntPtr session);
}