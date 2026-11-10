using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Symbols.Dia;

/// <summary>
/// Decode Symbols using DIA SDK to resolve RVAs to demangled function names
/// </summary>
public sealed class DiaResolver : IDisposable
{
    internal const int MaximumNameLength = 4096;

    /// <summary>
    /// Creates a cached resolver for the PDB at <paramref name="pdbPath"/>
    /// </summary>
    public DiaResolver(string pdbPath)
    {
        NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory,
                                        "runtimes",
                                        "win-x64",
                                        "native",
                                        "InternalsViewer.Query.DiaBridge.dll"));

        Session = DiaBridge.OpenPdb(pdbPath);

        if (Session == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Unable to open PDB '{pdbPath}'.");
        }
    }

    private IntPtr Session { get; }

    /// <summary>
    /// Resolve an RVA (Relative Virtual Address) to a demangled function name
    /// </summary>
    public unsafe string Resolve(uint rva)
    {
        Span<char> buffer = stackalloc char[MaximumNameLength];

        fixed (char* destination = buffer)
        {
            if (!DiaBridge.ResolveRva(Session, rva, destination, buffer.Length))
            {
                return $"0x{rva:X}";
            }
        }

        var length = buffer.IndexOf('\0');

        return new string(length < 0 ? buffer : buffer[..length]);
    }

    internal SymbolReader ReadSymbols(bool includeSignature = true, bool includeDecoratedName = false)
        => new(DiaBridge.BeginEnumSymbols(Session, string.Empty), includeSignature, includeDecoratedName);

    public void Dispose()
    {
        if (Session != IntPtr.Zero)
        {
            DiaBridge.ClosePdb(Session);
        }
    }
}
