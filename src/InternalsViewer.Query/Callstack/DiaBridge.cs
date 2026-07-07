using System.Runtime.InteropServices;
using System.Text;

namespace InternalsViewer.Query.Callstack;

internal static class DiaBridge
{
    [DllImport("InternalsViewer.Query.DiaBridge.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr OpenPdb(string pdbPath);

    [DllImport("InternalsViewer.Query.DiaBridge.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ResolveRva(IntPtr session,
                                         uint rva,
                                         StringBuilder buffer,
                                         int bufferLength);

    [DllImport("InternalsViewer.Query.DiaBridge.dll")]
    public static extern void ClosePdb(IntPtr session);
}