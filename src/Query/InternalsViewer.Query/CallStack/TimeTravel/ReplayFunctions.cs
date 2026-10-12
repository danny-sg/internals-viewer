using System.Collections.Concurrent;
using System.Diagnostics;
using InternalsViewer.Internals.Engine.Loading;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.Dia;
using InternalsViewer.Query.CallStack.Symbols;
using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public static class ReplayFunctions
{
    private static readonly string[] Modules = ["sqlmin", "sqllang", "sqldk"];

    private static readonly string[] HeapModules = ["ntdll", "kernelbase", "kernel32", "ucrtbase"];

    private static ConcurrentDictionary<(string Pdb, string Guid, int Age), ReplayFunctionSet> Cache { get; } = new();

    private static ConcurrentDictionary<(string Pdb, string Guid, int Age), Lazy<Task>> Resolving { get; } = new();

    public static Task PrepareAsync(IEnumerable<string> modulePaths,
                                    string symbolsPath,
                                    ILogger? logger,
                                    CancellationToken cancellationToken)
        => EnsureResolvedAsync([.. modulePaths.Select(p => TimeTravelModuleIdentity.Describe(new TimeTravelModule(p, 0, 0)))
                                              .Where(IsReplayed)],
                               symbolsPath,
                               logger,
                               null,
                               cancellationToken);

    public static async Task<ReplayFunctionSet> ResolveAsync(IReadOnlyList<TimeTravelModule> modules,
                                                             string symbolsPath,
                                                             ILogger? logger,
                                                             IProgress<ProgressDetail>? progress,
                                                             CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();

        var heap = modules.Where(m => HeapModules.Contains(Path.GetFileNameWithoutExtension(m.Path), StringComparer.OrdinalIgnoreCase))
                          .SelectMany(HeapFunctions)
                          .ToList();

        var identities = modules.Select(TimeTravelModuleIdentity.Describe)
                                .Where(IsReplayed)
                                .ToList();

        if (identities.Count == 0)
        {
            return new ReplayFunctionSet([], [.. heap], [], []);
        }

        var missing = identities.Count(m => !Cache.ContainsKey(KeyOf(m)));

        await EnsureResolvedAsync(identities, symbolsPath, logger, progress, cancellationToken);

        var found = identities.Select(m => Cache.TryGetValue(KeyOf(m), out var relative) ? relative.Rebase(m.Address) : null)
                              .OfType<ReplayFunctionSet>()
                              .ToList();

        logger?.LogInformation("Replay functions for {Modules} resolved in {Elapsed} ({Cached} of {Count} from the cache)",
                               string.Join(", ", identities.Select(m => m.Name)),
                               Stopwatch.GetElapsedTime(start),
                               identities.Count - missing,
                               identities.Count);

        return new ReplayFunctionSet([.. found.SelectMany(f => f.Excluded)],
                                     [.. found.SelectMany(f => f.Memory), .. heap],
                                     [.. found.SelectMany(f => f.InstanceMethods)],
                                     [.. found.SelectMany(f => f.Locators)]);
    }

    internal static bool IsExcluded(SymbolCategory category, string? iterator)
        => category is SymbolCategory.XEventInfrastructure || (category is SymbolCategory.Tracing && iterator is null);

    private static IEnumerable<MemoryFunction> HeapFunctions(TimeTravelModule module)
        => ModuleExports.Read(module.Path)
                        .Select(e => MemoryFunction.ClassifyExport(module.Address + e.Rva, e.Name))
                        .OfType<MemoryFunction>();

    private static (string Pdb, string Guid, int Age) KeyOf(TimeTravelModuleIdentity module) => (module.Pdb, module.Guid, module.Age);

    private static bool IsReplayed(TimeTravelModuleIdentity module)
        => module.Pdb.Length > 0 && Modules.Contains(module.Name, StringComparer.OrdinalIgnoreCase);

    private static Task EnsureResolvedAsync(IEnumerable<TimeTravelModuleIdentity> modules,
                                            string symbolsPath,
                                            ILogger? logger,
                                            IProgress<ProgressDetail>? progress,
                                            CancellationToken cancellationToken)
        => Task.WhenAll(modules.Where(m => !Cache.ContainsKey(KeyOf(m)))
                               .Select(m => ResolvingOf(m, symbolsPath, logger, progress)))
               .WaitAsync(cancellationToken);

    private static Task ResolvingOf(TimeTravelModuleIdentity module,
                                    string symbolsPath,
                                    ILogger? logger,
                                    IProgress<ProgressDetail>? progress)
        => Resolving.GetOrAdd(KeyOf(module),
                              key => new Lazy<Task>(() => Task.Run(() => DownloadAndResolveAsync(key,
                                                                                                 module,
                                                                                                 symbolsPath,
                                                                                                 logger,
                                                                                                 progress))))
                    .Value;

    private static async Task DownloadAndResolveAsync((string Pdb, string Guid, int Age) key,
                                                      TimeTravelModuleIdentity module,
                                                      string symbolsPath,
                                                      ILogger? logger,
                                                      IProgress<ProgressDetail>? progress)
    {
        try
        {
            await SymbolDownloader.DownloadSymbols([module.Frame(module.Address, 0)], symbolsPath, progress);

            Resolve(module, symbolsPath, logger);
        }
        finally
        {
            Resolving.TryRemove(key, out _);
        }
    }

    private static void Resolve(TimeTravelModuleIdentity module, string symbolsPath, ILogger? logger)
    {
        var start = Stopwatch.GetTimestamp();

        if (Find(module with { Address = 0 }, symbolsPath) is not { } relative)
        {
            logger?.LogWarning("Replay functions for {Module} not resolved, {Pdb} was not found", module.Name, module.Pdb);

            return;
        }

        Cache[KeyOf(module)] = relative;

        logger?.LogInformation("Replay functions for {Module} resolved from {Pdb} in {Elapsed}: {Excluded} excluded, "
                               + "{Iterators} iterator methods, {Locators} locator reads",
                               module.Name,
                               module.Pdb,
                               Stopwatch.GetElapsedTime(start),
                               relative.Excluded.Length,
                               relative.InstanceMethods.Length,
                               relative.Locators.Length);
    }

    private static ReplayFunctionSet? Find(TimeTravelModuleIdentity module, string symbolsPath)
    {
        var pdbPath = CallstackResolver.GetPdbPath(symbolsPath, module.Frame(module.Address, 0));

        if (!File.Exists(pdbPath))
        {
            return null;
        }

        using var resolver = new DiaResolver(pdbPath);

        var mappings = CategoryMappings.Default;

        var excluded = new HashSet<uint>();

        var kept = new HashSet<uint>();

        var memory = new Dictionary<uint, MemoryFunction?>();

        var iteratorMethods = IteratorMethods.AppliesTo(module.Name) ? new IteratorMethods() : null;

        var locatorFunctions = LocatorReads.AppliesTo(module.Name) ? new HashSet<uint>() : null;

        foreach (var symbol in resolver.EnumerateSymbolDetails(string.Empty, includeSignature: false))
        {
            if (!symbol.IsFunction)
            {
                continue;
            }

            iteratorMethods?.Add(symbol);

            locatorFunctions?.Add(symbol.Rva);

            var separator = ResolvedCallstackFrameParser.FindClassMethodSeparator(symbol.Name);

            var className = separator >= 0 ? symbol.Name[..separator] : null;

            var methodName = separator >= 0 ? symbol.Name[(separator + 2)..] : symbol.Name;

            var category = mappings.Classify(module.Name, className, methodName);

            var iterator = mappings.ClassifyOperator(module.Name, className, methodName).Iterator;

            (IsExcluded(category, iterator) ? excluded : kept).Add(symbol.Rva);

            var function = MemoryFunction.Classify(module.Address + symbol.Rva, symbol.Name);

            memory[symbol.Rva] = memory.TryGetValue(symbol.Rva, out var existing) && existing != function ? null : function;
        }

        excluded.ExceptWith(kept);

        locatorFunctions?.ExceptWith(excluded);

        return new ReplayFunctionSet([.. excluded.Select(rva => module.Address + rva)],
                                     [.. memory.Values.OfType<MemoryFunction>()],
                                     iteratorMethods is null
                                         ? []
                                         : [.. iteratorMethods.Resolve(resolver).Select(rva => module.Address + rva)],
                                     locatorFunctions is null ? [] : [.. LocatorReads.Find(resolver, module.Address, locatorFunctions)]);
    }
}
