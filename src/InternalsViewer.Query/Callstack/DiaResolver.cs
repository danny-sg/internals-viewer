using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace InternalsViewer.Query.Callstack;

public sealed class DiaResolver : IDisposable
{
    private readonly IntPtr _session;

    private readonly ConcurrentDictionary<uint, string> _cache = new();

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
    /// The demangled names of every public symbol in the PDB whose name starts with <paramref name="prefix"/>
    /// </summary>
    /// <remarks>
    /// Used to seed the operator mapping — e.g. enumerate everything beginning <c>CQ</c> to see the iterator classes.
    /// </remarks>
    public IEnumerable<string> EnumerateSymbols(string prefix)
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

    public void Dispose()
    {
        if (_session != IntPtr.Zero)
        {
            DiaBridge.ClosePdb(_session);
        }
    }
}