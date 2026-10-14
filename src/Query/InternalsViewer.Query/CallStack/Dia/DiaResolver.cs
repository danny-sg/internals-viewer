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
        => EnumerateDetails(DiaBridge.BeginEnumSymbols(_session, prefix), includeSignature, include: null);

    internal IEnumerable<SymbolDetail> EnumerateFunctions(Func<uint, bool> include)
        => EnumerateDetails(DiaBridge.BeginEnumSymbols(_session, string.Empty),
                            includeSignature: true,
                            (rva, isFunction) => isFunction && include(rva));

    /// <summary>
    /// Enumerate the symbols at one address
    /// </summary>
    internal IEnumerable<SymbolDetail> EnumerateSymbolsAtRva(uint rva)
        => EnumerateDetails(DiaBridge.BeginEnumSymbolsAtRva(_session, rva), includeSignature: true, include: null);

    internal IEnumerable<SymbolDetail> EnumerateFunctionsAtRva(uint rva, bool includeSignature = true)
        => EnumerateDetails(DiaBridge.BeginEnumSymbolsAtRva(_session, rva), includeSignature, (_, isFunction) => isFunction);

    internal unsafe void VisitFunctions(Action<uint, ReadOnlySpan<char>> visit)
    {
        var enumerator = DiaBridge.BeginEnumSymbols(_session, string.Empty);

        if (enumerator == IntPtr.Zero)
        {
            return;
        }

        try
        {
            Span<char> buffer = stackalloc char[MaximumNameLength];

            fixed (char* destination = buffer)
            {
                while (DiaBridge.NextSymbolDetail(enumerator, destination, buffer.Length, null, 0, out var rva, out var isFunction))
                {
                    var length = buffer.IndexOf('\0');

                    if (isFunction != 0 && length >= 0)
                    {
                        visit(rva, buffer[..length]);
                    }
                }
            }
        }
        finally
        {
            DiaBridge.EndEnumSymbols(enumerator);
        }
    }

    internal unsafe IReadOnlyList<string> SignaturesAt(uint rva)
    {
        var signatures = new List<string>();

        var enumerator = DiaBridge.BeginEnumSymbolsAtRva(_session, rva);

        if (enumerator == IntPtr.Zero)
        {
            return signatures;
        }

        try
        {
            Span<char> buffer = stackalloc char[MaximumNameLength];

            fixed (char* destination = buffer)
            {
                while (DiaBridge.NextSymbolDetail(enumerator, null, 0, destination, buffer.Length, out _, out var isFunction))
                {
                    var length = buffer.IndexOf('\0');

                    if (isFunction != 0 && length > 0)
                    {
                        signatures.Add(new string(buffer[..length]));
                    }
                }
            }
        }
        finally
        {
            DiaBridge.EndEnumSymbols(enumerator);
        }

        return signatures;
    }

    internal string? GetDecoratedName(uint rva, string? name = null)
    {
        Span<char> buffer = stackalloc char[MaximumNameLength];

        return TryGetDecoratedName(rva, buffer, out var decoratedName, name) ? new string(decoratedName) : null;
    }

    internal unsafe bool TryGetDecoratedName(uint rva, Span<char> buffer, out ReadOnlySpan<char> decoratedName, string? name = null)
    {
        decoratedName = default;

        fixed (char* destination = buffer)
        {
            if (!DiaBridge.GetDecoratedName(_session, rva, name, destination, buffer.Length))
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

    private static IEnumerable<SymbolDetail> EnumerateDetails(IntPtr enumerator, bool includeSignature, Func<uint, bool, bool>? include)
    {
        if (enumerator == IntPtr.Zero)
        {
            yield break;
        }

        try
        {
            var name = new char[MaximumNameLength];

            var signature = includeSignature ? new char[MaximumNameLength] : null;

            while (NextSymbolDetail(enumerator, name, signature, out var rva, out var isFunction))
            {
                if (include?.Invoke(rva, isFunction) == false)
                {
                    continue;
                }

                yield return new SymbolDetail(GetText(name), signature is null ? string.Empty : GetText(signature), rva, isFunction);
            }
        }
        finally
        {
            DiaBridge.EndEnumSymbols(enumerator);
        }
    }

    private static unsafe bool NextSymbolDetail(IntPtr enumerator, char[] name, char[]? signature, out uint rva, out bool isFunction)
    {
        fixed (char* namePointer = name)
        fixed (char* signaturePointer = signature)
        {
            var found = DiaBridge.NextSymbolDetail(enumerator,
                                                   namePointer,
                                                   name.Length,
                                                   signaturePointer,
                                                   signature?.Length ?? 0,
                                                   out rva,
                                                   out var function);

            isFunction = function != 0;

            return found;
        }
    }

    private static string GetText(char[] buffer)
    {
        var length = Array.IndexOf(buffer, '\0');

        return new string(buffer, 0, length < 0 ? buffer.Length : length);
    }

    public void Dispose()
    {
        if (_session != IntPtr.Zero)
        {
            DiaBridge.ClosePdb(_session);
        }
    }
}