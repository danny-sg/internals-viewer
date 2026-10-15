using System.Collections.Concurrent;
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
        => EnumerateDetails(DiaBridge.BeginEnumSymbols(_session, prefix), includeSignature, functionsOnly: false);

    /// <summary>
    /// Enumerate the symbols at one address
    /// </summary>
    internal IEnumerable<SymbolDetail> EnumerateSymbolsAtRva(uint rva)
        => EnumerateDetails(DiaBridge.BeginEnumSymbolsAtRva(_session, rva), includeSignature: true, functionsOnly: false);

    internal IEnumerable<SymbolDetail> EnumerateFunctionsAtRva(uint rva, bool includeSignature = true)
        => EnumerateDetails(DiaBridge.BeginEnumSymbolsAtRva(_session, rva), includeSignature, functionsOnly: true);

    internal SymbolReader ReadSymbols(bool includeSignature = true)
        => new(DiaBridge.BeginEnumSymbols(_session, string.Empty), includeSignature);

    internal SymbolReader ReadSymbolsAtRva(uint rva, bool includeSignature = true)
        => new(DiaBridge.BeginEnumSymbolsAtRva(_session, rva), includeSignature);

    internal string? GetSignature(uint rva)
    {
        using var symbols = ReadSymbolsAtRva(rva);

        while (symbols.MoveNext())
        {
            if (symbols.IsFunction && !symbols.Signature.IsEmpty)
            {
                return symbols.Signature.ToString();
            }
        }

        return null;
    }

    internal string? GetDecoratedName(uint rva, ReadOnlySpan<char> name = default)
    {
        Span<char> buffer = stackalloc char[MaximumNameLength];

        return TryGetDecoratedName(rva, buffer, out var decoratedName, name) ? new string(decoratedName) : null;
    }

    internal unsafe bool TryGetDecoratedName(uint rva,
                                             Span<char> buffer,
                                             out ReadOnlySpan<char> decoratedName,
                                             ReadOnlySpan<char> name = default)
    {
        decoratedName = default;

        Span<char> terminatedName = stackalloc char[name.Length + 1];

        name.CopyTo(terminatedName);

        terminatedName[^1] = '\0';

        fixed (char* namePointer = terminatedName)
        fixed (char* destination = buffer)
        {
            if (!DiaBridge.GetDecoratedName(_session, rva, namePointer, destination, buffer.Length))
            {
                return false;
            }
        }

        var length = buffer.IndexOf('\0');

        if (length < 0)
        {
            return false;
        }

        decoratedName = buffer[..length];

        return true;
    }

    private static IEnumerable<SymbolDetail> EnumerateDetails(IntPtr enumerator, bool includeSignature, bool functionsOnly)
    {
        using var symbols = new SymbolReader(enumerator, includeSignature);

        while (symbols.MoveNext())
        {
            if (functionsOnly && !symbols.IsFunction)
            {
                continue;
            }

            yield return new SymbolDetail(symbols.Name.ToString(), symbols.Signature.ToString(), symbols.Rva, symbols.IsFunction);
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
