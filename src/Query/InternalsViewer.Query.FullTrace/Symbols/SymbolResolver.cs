using System.Collections.Concurrent;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;
using InternalsViewer.Query.FullTrace.Symbols.Dia;
using InternalsViewer.Query.FullTrace.Symbols.Enums;

namespace InternalsViewer.Query.FullTrace.Symbols;

/// <summary>
/// Process the callstack by using the debugging symbols to resolve function names
/// </summary>
/// <remarks>
/// This is a pretty complicated process that requires an additional C++ DLL to integrate the symbols
///
/// The SQL Server callstack provides:
///
///     Module - Name of the SQL Server module that generated the frame
///     PDB    - Program Database - provides the mapping between the compiled binary and the source code
///     Guid   - Unique identifier with Age for the version of the PDB required
///     Age    - PDB revision number
///     RVA    - Relative Virtual Address - the offset of the function in the binary
///
/// First symbols are downloaded from the Microsoft symbol server and cached locally. A path is constructed from PDB\Guid+Age\PDB. This
/// path identifies the correct file from the symbols server and is replicated in the local cache.
///
/// Symbols are then resolved using the Debug Interface Access (DIA) API. DIA allows the mapping of the RVA to the function name with the
/// .PDB files.
///
/// To mitigate the need for the DIA SDK to be installed separately on every deployment the redistributable .dll, msdia140.dll is included.
/// Note - msdia140.dll is specifically listed as an allowed redistributable component -see:
///     https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution
///
/// DIA is a COM API and requires the msdia140.dll to be registered. Rather than requiring the user to register the DLL manually we use
/// InternalsViewer.Query.DiaBridge to provide registration-free access to DIA that can be P/Invoke'd from C#. This is a bit of a faff but
/// is the cleanest way to access DIA without quite heavyweight requirements, e.g. installing Build Tools.
/// </remarks>
public sealed class SymbolResolver(string symbolsPath) : IDisposable
{
    private const string StartOffset = "+0x0";

    private readonly record struct SymbolKey(string Guid, int Age, uint Rva);

    private static readonly ConcurrentDictionary<SymbolKey, string> TableNames = new();

    private static readonly ConcurrentDictionary<SymbolKey, string> DiaNames = new();

    public IReadOnlyCollection<string> UnknownSymbols => UnknownSymbolSet;

    private ConcurrentDictionary<string, DiaResolver> ResolverCache { get; } = new();

    private HashSet<string> UnknownSymbolSet { get; } = new(StringComparer.Ordinal);

    private bool IsDisposed { get; set; }

    public bool TryResolve(CallFrame frame, out string? symbol)
    {
        symbol = null;

        var key = new SymbolKey(frame.Module.Guid, frame.Module.Age, frame.Rva);

        if (!frame.Module.HasSymbols)
        {
            return false;
        }

        symbol = ResolveInternal(frame, key);

        return true;
    }

    public void ResolveFunctions(IEnumerable<CallFrame> frames)
    {
        var functions = new FrameFunctionLookup();

        foreach (var frame in frames)
        {
            if (!TryResolve(frame, out var symbol) || string.IsNullOrEmpty(symbol))
            {
                continue;
            }

            functions.Resolve(frame, symbol);

            if (frame.Module.Category == ModuleCategory.Unknown || frame.Function is { SymbolCategory: SymbolCategory.Unknown })
            {
                UnknownSymbolSet.Add(frame.QualifiedName);
            }
        }
    }

    /// <summary>
    /// Lists the members of a class from the PDBs of every frame given, one group per module in the order given
    /// </summary>
    /// <remarks>
    /// Modules whose symbols declare nothing on the class are left out. Each module is searched on its own thread as
    /// the first search of a module indexes its whole PDB.
    /// </remarks>
    public async Task<ClassMemberListing> ListMembersAsync(IReadOnlyList<CallFrame> frames, string className)
    {
        var groups = await Task.WhenAll(frames.Select(f => Task.Run(() => ListMembers(f, className))));

        return new ClassMemberListing(className, groups.Where(g => g.Members.Count > 0).ToList());
    }

    /// <summary>
    /// Finds the public symbols whose name contains the text, across the PDBs of every frame given
    /// </summary>
    /// <remarks>
    /// Each module is searched on its own thread, as the first search of a module packs its whole PDB into an index.
    /// The limit applies per module, so a common word still shows something from each.
    /// </remarks>
    public async Task<IReadOnlyList<SymbolMatch>> SearchSymbolsAsync(IReadOnlyList<CallFrame> frames,
                                                                    string text,
                                                                    SymbolSearchFields fields,
                                                                    int limit)
    {
        var groups = await Task.WhenAll(frames.Select(f => Task.Run(() => SearchSymbols(f, text, fields, limit))));

        return groups.SelectMany(g => g).ToList();
    }

    /// <summary>
    /// Finds the public symbols in the frame's PDB whose chosen fields contain the text
    /// </summary>
    /// <remarks>
    /// Text with <c>|</c> in it is several searches, any of which qualifies a symbol. Within each, a <c>module!</c>
    /// prefix, as WinDbg writes a symbol, confines the search to modules matching it and searches the rest. Otherwise
    /// the module field is decided here: when it is searched and the module's name contains the text, the first
    /// symbols of the module qualify regardless of their own names.
    /// </remarks>
    public IReadOnlyList<SymbolMatch> SearchSymbols(CallFrame frame, string text, SymbolSearchFields fields, int limit)
    {
        if (SymbolTableCache.Get(symbolsPath, frame.Module) is not { } table)
        {
            return [];
        }

        var index = table.GetSearchIndex();

        return text.Split(SymbolSearchSuggestion.Alternative, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .SelectMany(alternative => SearchAlternative(frame, index, alternative, fields, limit))
                   .DistinctBy(m => (m.ClassName, m.Member.Signature))
                   .ToList();
    }

    private static List<SymbolMatch> SearchAlternative(CallFrame frame,
                                                       SymbolIndex index,
                                                       string text,
                                                       SymbolSearchFields fields,
                                                       int limit)
    {
        var (modulePart, symbolPart) = SymbolIndex.SplitModule(text);

        if (modulePart is not null && !SymbolIndex.ModuleMatches(modulePart, frame.Module.Name))
        {
            return [];
        }

        var moduleMatches = symbolPart.Length == 0
                            || (modulePart is null
                                && (fields == SymbolSearchFields.None || fields.HasFlag(SymbolSearchFields.Module))
                                && frame.Module.Name.Contains(symbolPart, StringComparison.OrdinalIgnoreCase));

        var hits = moduleMatches ? index.GetFirst(limit) : index.Search(symbolPart, fields, limit);

        var matches = new List<SymbolMatch>();

        foreach (var symbol in hits)
        {
            var className = symbol.ClassName;

            var prefix = className.Length > 0 ? $"{className}::" : string.Empty;

            matches.Add(new SymbolMatch(className,
                                        new ClassMember(frame.Module.Name,
                                                        symbol.MethodName,
                                                        RemoveScope(symbol.Signature, prefix),
                                                        symbol.Rva,
                                                        symbol.IsFunction)));
        }

        return matches;
    }

    /// <summary>
    /// Lists the members the frame's PDB declares on a class
    /// </summary>
    /// <remarks>
    /// Compiler-generated entries such as the vftable and the deleting destructors are left out.
    /// </remarks>
    public ClassMemberGroup ListMembers(CallFrame frame, string className)
    {
        if (SymbolTableCache.Get(symbolsPath, frame.Module) is not { } table)
        {
            return new ClassMemberGroup(frame.Module.Name, []);
        }

        var prefix = $"{className}::";

        var members = table.GetClassMembers(className)
                           .Select(s => new ClassMember(frame.Module.Name,
                                                        s.MethodName,
                                                        RemoveScope(s.Signature, prefix),
                                                        s.Rva,
                                                        s.IsFunction))
                           .DistinctBy(m => m.Signature, StringComparer.Ordinal)
                           .OrderBy(m => m.Signature, StringComparer.Ordinal)
                           .ToList();

        return new ClassMemberGroup(frame.Module.Name, members);
    }

    public Symbol? ResolveSymbol(CallFrame frame)
        => SymbolTableCache.Get(symbolsPath, frame.Module)?.FindFunction(GetFunctionRva(frame), frame.Function?.Name);

    public void ResolveSignatures(IEnumerable<CallFrame> frames)
    {
        var functions = frames.Where(f => f.Function is { Signature: null } && f.Module.HasSymbols)
                              .GroupBy(f => (f.Module.Pdb, f.Module.Guid, f.Module.Age, Rva: GetFunctionRva(f)));

        foreach (var function in functions)
        {
            if (ResolveSymbol(function.First()) is not { } symbol || symbol.Signature is not { Length: > 0 } signature)
            {
                continue;
            }

            var scoped = symbol.ClassName is { Length: > 0 } className ? RemoveScope(signature, $"{className}::") : signature;

            if (symbol.Parsed is { Type: FunctionType.Static })
            {
                scoped = $"static {scoped}";
            }

            foreach (var frame in function)
            {
                frame.Function!.Signature = scoped;
            }
        }
    }

    private static uint GetFunctionRva(CallFrame frame)
        => frame.Offset is { } offset && offset <= frame.Rva ? frame.Rva - offset : frame.Rva;

    private static string RemoveScope(string signature, string prefix)
    {
        var index = signature.IndexOf(prefix, StringComparison.Ordinal);

        var scoped = index < 0 ? signature : signature.Remove(index, prefix.Length);

        return scoped.Replace("(void)", "()", StringComparison.Ordinal);
    }

    private string ResolveInternal(CallFrame frame, SymbolKey key)
    {
        if (SymbolTableCache.Find(frame.Module)?.FindFunction(frame.Rva) is { } symbol)
        {
            return TableNames.GetOrAdd(key, static (_, found) => found.Name + StartOffset, symbol);
        }

        if (DiaNames.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var pdbPath = frame.Module.GetPdbPath(symbolsPath);

        if (!File.Exists(pdbPath))
        {
            return $"0x{frame.Rva:X}";
        }

        var resolver = ResolverCache.GetOrAdd(pdbPath, path => new DiaResolver(path));

        return DiaNames.GetOrAdd(key, resolver.Resolve(frame.Rva));
    }

    public void Dispose()
    {
        if(IsDisposed)
        {
            return;
        }

        foreach (var resolver in ResolverCache.Values)
        {
            resolver.Dispose();
        }

        ResolverCache.Clear();

        IsDisposed = true;
    }
}

