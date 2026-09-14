using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace InternalsViewer.Query.CallStack.Dia;

/// <summary>
/// Decode Symbols using DIA SDK to resolve RVAs to demangled function names
/// </summary>
public sealed class DiaResolver : IDisposable
{
    private readonly IntPtr _session;

    private readonly ConcurrentDictionary<uint, string> _cache = new();

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
    public string Resolve(uint rva)
    {
        return _cache.GetOrAdd(rva, ResolveInternal);
    }

    private string ResolveInternal(uint rva)
    {
        var buffer = new StringBuilder(4096);

        var success = DiaBridge.ResolveRva(_session,
                                           rva,
                                           buffer,
                                           buffer.Capacity);

        if (!success)
        {
            return $"0x{rva:X}";
        }

        return buffer.ToString();
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

    /// <summary>
    /// Enumerate all symbols with a given prefix, with their address and kind, and their signature when asked for
    /// </summary>
    internal IEnumerable<SymbolDetail> EnumerateSymbolDetails(string prefix, bool includeSignature = true)
        => EnumerateDetails(DiaBridge.BeginEnumSymbols(_session, prefix), includeSignature);

    /// <summary>
    /// Enumerate the symbols at one address
    /// </summary>
    internal IEnumerable<SymbolDetail> EnumerateSymbolsAtRva(uint rva)
        => EnumerateDetails(DiaBridge.BeginEnumSymbolsAtRva(_session, rva), includeSignature: true);

    private static IEnumerable<SymbolDetail> EnumerateDetails(IntPtr enumerator, bool includeSignature)
    {
        if (enumerator == IntPtr.Zero)
        {
            yield break;
        }

        try
        {
            var name = new StringBuilder(4096);

            var signature = includeSignature ? new StringBuilder(4096) : null;

            while (DiaBridge.NextSymbolDetail(enumerator,
                                              name,
                                              name.Capacity,
                                              signature,
                                              signature?.Capacity ?? 0,
                                              out var rva,
                                              out var isFunction))
            {
                yield return new SymbolDetail(name.ToString(), signature?.ToString() ?? string.Empty, rva, isFunction != 0);
            }
        }
        finally
        {
            DiaBridge.EndEnumSymbols(enumerator);
        }
    }

    public void Dispose()
    {
        if (_session != IntPtr.Zero)
        {
            DiaBridge.ClosePdb(_session);
        }
    }
}