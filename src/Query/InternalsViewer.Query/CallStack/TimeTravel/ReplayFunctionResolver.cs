using System.Collections.Concurrent;
using System.Diagnostics;
using InternalsViewer.Internals.Engine.Loading;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.Dia;
using InternalsViewer.Query.CallStack.Symbols;
using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;
using InternalsViewer.Query.Helpers;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query.CallStack.TimeTravel;

/// <summary>
/// Resolves a ReplayFunctionSet for the trace
/// </summary>
/// <remarks>
/// ReplayFunctionSet provides functions that are excluded from the call stack, memory functions, instance methods, and locator reads per
/// module.
///
/// Results are prepared and cached per module as the derivation of the function sets is expensive, but won't change from run to run.
///
/// To make decisions about the category of a function debug symbols must translate from a module + RVA to a function name. This is done by
/// downloading the symbols and using the DIA bridge for name resolution.
///
/// Exclusions
///
///     Excluded functions based on mapped category (e.g. XEventInfrastructure, Tracing) using CategoryMappings.
///
/// Memory Functions
///
///     Memory allocation and deallocations functions.
///
///     SQL Server memory functions are identified by mapped category.
///
///     Windows heap + C Runtime memory function names are resolved using ModuleExportReader that reads the PE export table to get the name
///     from an address and classifies the function via MemoryFunction.ClassifyExport. The reason this is done instead of downloading
///     symbols is that first the data is available directly, and second a Windows Update could bump the age of the module requiring
///     frequent downloads of new files.
///
///    are identified via MemoryFunction.ClassifyExport.
///
/// Instance Methods
///
///     Functions keyed from a class instance pointer. These are identified via InstanceMethodCollector that uses CategoryMappings to
///     identify the class and method.
///
/// Locators
///
///     Functions that read page/row id via their arguments.
///     
///     
/// </remarks>
internal static class ReplayFunctionResolver
{
    private static readonly string[] Modules = ["sqlmin", "sqllang", "sqldk"];

    private static readonly string[] HeapModules = ["ntdll", "kernelbase", "kernel32", "ucrtbase"];

    private static ConcurrentDictionary<(string Pdb, string Guid, int Age), ReplayFunctionSet> Cache { get; } = new();

    private static ConcurrentDictionary<(string Pdb, string Guid, int Age), Lazy<Task>> Resolving { get; } = new();

    public static Task PrepareAsync(IEnumerable<string> modulePaths,
                                    string symbolsPath,
                                    ILogger? logger,
                                    CancellationToken cancellationToken)
        => EnsureResolvedAsync([.. modulePaths.Select(p => TimeTravelModule.Read(p, 0, 0)).Where(IsReplayed)],
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

        var identities = modules.Where(IsReplayed).ToList();

        if (identities.Count == 0)
        {
            return new ReplayFunctionSet([], [.. heap], [], []);
        }

        var missing = identities.Count(m => !Cache.ContainsKey(KeyOf(m)));

        await EnsureResolvedAsync(identities, symbolsPath, logger, progress, cancellationToken);

        var found = identities.Select(m => Cache.TryGetValue(KeyOf(m), out var relative) ? relative.Rebase(m.StartAddress) : null)
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
        => ModuleExportReader.Read(module.Path)
                             .Select(e => MemoryFunction.ClassifyExport(module.StartAddress + e.Rva, e.Name))
                             .OfType<MemoryFunction>();

    private static (string Pdb, string Guid, int Age) KeyOf(TimeTravelModule module) => (module.Pdb, module.Guid, module.Age);

    private static bool IsReplayed(TimeTravelModule module)
        => module.Pdb.Length > 0 && Modules.Contains(module.Name, StringComparer.OrdinalIgnoreCase);

    private static Task EnsureResolvedAsync(IEnumerable<TimeTravelModule> modules,
                                            string symbolsPath,
                                            ILogger? logger,
                                            IProgress<ProgressDetail>? progress,
                                            CancellationToken cancellationToken)
        => Task.WhenAll(modules.Where(m => !Cache.ContainsKey(KeyOf(m)))
                               .Select(m => ResolvingOf(m, symbolsPath, logger, progress)))
               .WaitAsync(cancellationToken);

    private static Task ResolvingOf(TimeTravelModule module,
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
                                                      TimeTravelModule module,
                                                      string symbolsPath,
                                                      ILogger? logger,
                                                      IProgress<ProgressDetail>? progress)
    {
        try
        {
            await SymbolDownloader.DownloadSymbols([module.Frame(module.StartAddress, 0)], symbolsPath, progress);

            Resolve(module, symbolsPath, logger);
        }
        finally
        {
            Resolving.TryRemove(key, out _);
        }
    }

    private static void Resolve(TimeTravelModule module, string symbolsPath, ILogger? logger)
    {
        var start = Stopwatch.GetTimestamp();

        if (Find(module with { StartAddress = 0 }, symbolsPath) is not { } relative)
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

    private static ReplayFunctionSet? Find(TimeTravelModule module, string symbolsPath)
    {
        var pdbPath = CallstackResolver.GetPdbPath(symbolsPath, module.Frame(module.StartAddress, 0));

        if (!File.Exists(pdbPath))
        {
            return null;
        }

        using var resolver = new DiaResolver(pdbPath);

        var mappings = CategoryMappings.Default;

        var excluded = new HashSet<uint>();

        var kept = new HashSet<uint>();

        var memory = new Dictionary<uint, MemoryFunction?>();

        var instanceMethods = InstanceMethodCollector.AppliesTo(module.Name) ? new InstanceMethodCollector() : null;

        var locatorFunctions = LocatorReadResolver.AppliesTo(module.Name) ? new HashSet<uint>() : null;

        foreach (var symbol in resolver.EnumerateSymbolDetails(string.Empty, includeSignature: false))
        {
            if (!symbol.IsFunction)
            {
                continue;
            }

            instanceMethods?.Add(symbol);

            locatorFunctions?.Add(symbol.Rva);

            var separator = ResolvedCallstackFrameParser.FindClassMethodSeparator(symbol.Name);

            var className = separator >= 0 ? symbol.Name[..separator] : null;

            var methodName = separator >= 0 ? symbol.Name[(separator + 2)..] : symbol.Name;

            var category = mappings.Classify(module.Name, className, methodName);

            var iterator = mappings.ClassifyOperator(module.Name, className, methodName).Iterator;

            (IsExcluded(category, iterator) ? excluded : kept).Add(symbol.Rva);

            var function = MemoryFunction.Classify(module.StartAddress + symbol.Rva, symbol.Name);

            memory[symbol.Rva] = memory.TryGetValue(symbol.Rva, out var existing) && existing != function ? null : function;
        }

        excluded.ExceptWith(kept);

        locatorFunctions?.ExceptWith(excluded);

        return new ReplayFunctionSet([.. excluded.Select(rva => module.StartAddress + rva)],
                                     [.. memory.Values.OfType<MemoryFunction>()],
                                     instanceMethods is null
                                         ? []
                                         : [.. instanceMethods.Resolve(resolver)
                                                              .Select(m => m with { Address = module.StartAddress + m.Address })],
                                     locatorFunctions is null
                                         ? []
                                         : [.. LocatorReadResolver.Find(resolver, module.StartAddress, locatorFunctions)]);
    }
}
