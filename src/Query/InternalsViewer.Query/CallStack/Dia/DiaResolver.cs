using System.Runtime.InteropServices;
using System.Text;

namespace InternalsViewer.Query.CallStack.Dia;

/// <summary>
/// Decode Symbols using DIA SDK to resolve RVAs to demangled function names
/// </summary>
public sealed class DiaResolver : IDisposable
{
    internal const int MaximumNameLength = 4096;

    private readonly IntPtr _session;

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

        _session = DiaBridge.OpenPdb(pdbPath);

        if (_session == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Unable to open PDB '{pdbPath}'.");
        }
    }

    /// <summary>
    /// Resolve an RVA (Relative Virtual Address) to a demangled function name
    /// </summary>
    public unsafe string Resolve(uint rva)
    {
        Span<char> buffer = stackalloc char[MaximumNameLength];

        fixed (char* destination = buffer)
        {
            if (!DiaBridge.ResolveRva(_session, rva, destination, buffer.Length))
            {
                return $"0x{rva:X}";
            }
        }

        var length = buffer.IndexOf('\0');

        return new string(length < 0 ? buffer : buffer[..length]);
    }

    /// <summary>
    /// Enumerate all symbols with a given prefix
    /// </summary>
    internal IEnumerable<string> EnumerateSymbols(string prefix)
    {
        var enumerator = DiaBridge.BeginEnumSymbols(_session, prefix);

        if (enumerator == IntPtr.Zero)
        {
            yield break;
        }

        try
        {
            var buffer = new StringBuilder(4096);

            while (DiaBridge.NextSymbol(enumerator, buffer, buffer.Capacity))
            {
                yield return buffer.ToString();
            }
        }
        finally
        {
            DiaBridge.EndEnumSymbols(enumerator);
        }
    }

    internal SymbolReader ReadSymbols(bool includeSignature = true, bool includeDecoratedName = false)
        => new(DiaBridge.BeginEnumSymbols(_session, string.Empty), includeSignature, includeDecoratedName);

    public void Dispose()
    {
        if (_session != IntPtr.Zero)
        {
            DiaBridge.ClosePdb(_session);
        }
    }
}
