using InternalsViewer.Internals.Engine.Loading;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.Dia;
using InternalsViewer.Query.CallStack.Symbols;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public static class ReplayFunctions
{
    private const string PackageSeparator = "Pkg::";

    private const string PublishMethod = "Publish";

    private const string BufferManager = "XE_BufferMgr";

    private const string ReserveMethod = "Reserve";

    private static readonly string[] Modules = ["sqlmin", "sqllang", "sqldk"];

    private static readonly string[] HeapModules = ["ntdll", "kernelbase", "kernel32", "ucrtbase"];

    public static async Task<ReplayFunctionSet> ResolveAsync(IReadOnlyList<TimeTravelModule> modules,
                                                             string symbolsPath,
                                                             IProgress<ProgressDetail>? progress,
                                                             CancellationToken cancellationToken)
    {
        var heap = modules.Where(m => HeapModules.Contains(Path.GetFileNameWithoutExtension(m.Path), StringComparer.OrdinalIgnoreCase))
                          .SelectMany(HeapFunctions)
                          .ToList();

        var identities = modules.Select(TimeTravelModuleIdentity.Describe)
                                .Where(m => m.Pdb.Length > 0 && Modules.Contains(m.Name, StringComparer.OrdinalIgnoreCase))
                                .ToList();

        if (identities.Count == 0)
        {
            return new ReplayFunctionSet([], [.. heap], new Dictionary<ulong, string>(), []);
        }

        await SymbolDownloader.DownloadSymbols([.. identities.Select(m => m.Frame(m.Address, 0))],
                                               symbolsPath,
                                               progress,
                                               cancellationToken);

        var found = await Task.Run(() => identities.AsParallel()
                                                   .WithCancellation(cancellationToken)
                                                   .Select(m => Find(m, symbolsPath))
                                                   .ToList(),
                                   cancellationToken);

        return new ReplayFunctionSet([.. found.SelectMany(f => f.Excluded)],
                                     [.. found.SelectMany(f => f.Memory), .. heap],
                                     found.SelectMany(f => f.Publishers).ToDictionary(p => p.Key, p => p.Value),
                                     [.. found.SelectMany(f => f.BufferReserves)]);
    }

    internal static bool IsExcluded(SymbolCategory category)
        => category is SymbolCategory.XEventInfrastructure or SymbolCategory.Tracing;

    private static IEnumerable<MemoryFunction> HeapFunctions(TimeTravelModule module)
        => ModuleExports.Read(module.Path)
                        .Select(e => MemoryFunction.ClassifyExport(module.Address + e.Rva, e.Name))
                        .OfType<MemoryFunction>();

    private static ReplayFunctionSet Find(TimeTravelModuleIdentity module, string symbolsPath)
    {
        var pdbPath = CallstackResolver.GetPdbPath(symbolsPath, module.Frame(module.Address, 0));

        if (!File.Exists(pdbPath))
        {
            return new ReplayFunctionSet([], [], new Dictionary<ulong, string>(), []);
        }

        using var resolver = new DiaResolver(pdbPath);

        var mappings = CategoryMappings.Default;

        var excluded = new HashSet<uint>();

        var kept = new HashSet<uint>();

        var memory = new Dictionary<uint, MemoryFunction?>();

        var publishers = new Dictionary<uint, string?>();

        var reserves = new HashSet<uint>();

        foreach (var symbol in resolver.EnumerateSymbolDetails(string.Empty, includeSignature: false))
        {
            if (!symbol.IsFunction)
            {
                continue;
            }

            var separator = ResolvedCallstackFrameParser.FindClassMethodSeparator(symbol.Name);

            var className = separator >= 0 ? symbol.Name[..separator] : null;

            var methodName = separator >= 0 ? symbol.Name[(separator + 2)..] : symbol.Name;

            (IsExcluded(mappings.Classify(module.Name, className, methodName)) ? excluded : kept).Add(symbol.Rva);

            var function = MemoryFunction.Classify(module.Address + symbol.Rva, symbol.Name);

            memory[symbol.Rva] = memory.TryGetValue(symbol.Rva, out var existing) && existing != function ? null : function;

            if (ExtendedEventOf(className, methodName) is { } eventName)
            {
                publishers[symbol.Rva] = publishers.TryGetValue(symbol.Rva, out var existingEvent) && existingEvent != eventName
                    ? null
                    : eventName;
            }
            else if (className == BufferManager && methodName == ReserveMethod)
            {
                reserves.Add(symbol.Rva);
            }
        }

        excluded.ExceptWith(kept);

        return new ReplayFunctionSet([.. excluded.Select(rva => module.Address + rva)],
                                     [.. memory.Values.OfType<MemoryFunction>()],
                                     publishers.Where(p => p.Value is not null).ToDictionary(p => module.Address + p.Key, p => p.Value!),
                                     [.. reserves.Select(rva => module.Address + rva)]);
    }

    private static string? ExtendedEventOf(string? className, string methodName)
    {
        if (methodName != PublishMethod || className is null)
        {
            return null;
        }

        var package = className.IndexOf(PackageSeparator, StringComparison.Ordinal);

        if (package <= 0)
        {
            return null;
        }

        var eventName = className[(package + PackageSeparator.Length)..];

        return eventName.Length > 0 && !eventName.Contains("::", StringComparison.Ordinal) ? eventName : null;
    }
}
