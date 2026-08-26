using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.Tests;

/// <summary>
/// Resolves every function a binary declares in its exception directory, giving a name for each address
/// </summary>
/// <remarks>
/// A public PDB carries names and a stripped symbol table, so a name can be read back from an address but not the
/// other way about. The .pdata section lists the start of every function in the image, so resolving each one builds
/// the index the other way and validates it at the same time: an address that resolves came from a real function.
/// </remarks>
/// <remarks>
/// The buffer is far larger than <see cref="DiaResolver"/> passes because sqlmin carries demangled names of several
/// thousand characters, and the native side formats with swprintf_s, which terminates the process rather than
/// truncating when the buffer is too small.
/// </remarks>
public class SymbolIndexTests
{
    private const string Pdb = @"C:\Symbols\sqlmin.pdb\0F0CBB2ABB8040D69CC36FB2CDB380602\sqlmin.pdb";

    private const int BufferLength = 1 << 20;

    private static readonly string Starts = Path.Combine(Path.GetTempPath(), "starts.txt");

    private static readonly string Index = Path.Combine(Path.GetTempPath(), "sqlmin-symbols.txt");

    [DllImport("InternalsViewer.Query.DiaBridge.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenPdb(string pdbPath);

    [DllImport("InternalsViewer.Query.DiaBridge.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool ResolveRva(IntPtr session, uint rva, StringBuilder buffer, int bufferLength);

    [DllImport("InternalsViewer.Query.DiaBridge.dll")]
    private static extern void ClosePdb(IntPtr session);

    [Fact]
    public void Build_Symbol_Index()
    {
        if (!File.Exists(Pdb) || !File.Exists(Starts))
        {
            return;
        }

        // Constructing the resolver is what loads the native library out of the runtimes folder
        using var loader = new DiaResolver(Pdb);

        var session = OpenPdb(Pdb);

        Assert.NotEqual(IntPtr.Zero, session);

        var buffer = new StringBuilder(BufferLength);

        var resolved = 0;

        try
        {
            using var writer = new StreamWriter(Index);

            foreach (var line in File.ReadLines(Starts))
            {
                var rva = uint.Parse(line, NumberStyles.HexNumber);

                buffer.Clear();

                if (!ResolveRva(session, rva, buffer, BufferLength))
                {
                    continue;
                }

                writer.WriteLine($"{rva:X}\t{buffer}");

                resolved++;
            }
        }
        finally
        {
            ClosePdb(session);
        }

        Assert.True(resolved > 100000, $"only {resolved} resolved");
    }
}
