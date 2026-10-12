using InternalsViewer.Query.CallStack;
using System.Collections.Concurrent;
using System.Diagnostics;
using InternalsViewer.Internals.Engine.Loading;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.Dia;
using InternalsViewer.Query.CallStack.Symbols;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Markers;
using InternalsViewer.Query.FullTrace.Memory;
using Microsoft.Extensions.Logging;
using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace;

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
    private const string PackageSeparator = "Pkg::";

    private const string PublishMethod = "Publish";

    private const string BufferManager = "XE_BufferMgr";

    private const string ReserveMethod = "Reserve";

    private const string IteratorModule = "sqlmin";

    private static readonly string[] Modules = ["sqlmin", "sqllang", "sqldk"];

    private static readonly string[] HeapModules = ["ntdll", "kernelbase", "kernel32", "ucrtbase"];

    private static ConcurrentDictionary<(string Pdb, string Guid, int Age), ModuleFunctions> Cache { get; } = new();

    private static ConcurrentDictionary<(string Pdb, string Guid, int Age), Lazy<Task>> Resolving { get; } = new();

    public static Task PrepareAsync(IEnumerable<string> modulePaths,
                                    string symbolsPath,
                                    ILogger? logger,
                                    CancellationToken cancellationToken)
        => EnsureResolvedAsync([.. modulePaths.Select(p => TraceModule.Read(p, 0, 0)).Where(IsReplayed)],
                               symbolsPath,
                               logger,
                               null,
                               cancellationToken);

    public static async Task<(ReplayFunctionSet Replay, IReadOnlyList<MemoryFunction> Memory)> ResolveAsync(
        IReadOnlyList<TraceModule> modules,
        string symbolsPath,
        ILogger? logger,
        IProgress<ProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();

        List<MemoryFunction> heap = [.. modules.Where(IsHeap).SelectMany(HeapFunctions)];

        var identities = modules.Where(IsReplayed).ToList();

        if (identities.Count == 0)
        {
            return (ReplayFunctionSet.Empty, heap);
        }

        var missing = identities.Count(m => !Cache.ContainsKey(KeyOf(m)));

        await EnsureResolvedAsync(identities, symbolsPath, logger, progress, cancellationToken);

        var found = identities.Select(m => Cache.TryGetValue(KeyOf(m), out var relative) ? relative.Rebase(m.StartAddress) : null)
                              .OfType<ModuleFunctions>()
                              .ToList();

        logger?.LogInformation("Replay functions for {Modules} resolved in {Elapsed} ({Cached} of {Count} from the cache)",
                               string.Join(", ", identities.Select(m => m.Name)),
                               Stopwatch.GetElapsedTime(start),
                               identities.Count - missing,
                               identities.Count);

        return (ReplayFunctionSet.Merge([.. found.Select(f => f.Replay)]), [.. found.SelectMany(f => f.Memory), .. heap]);
    }

    internal static bool IsExcluded(SymbolCategory category, string? iterator)
        => category is SymbolCategory.XEventInfrastructure || (category is SymbolCategory.Tracing && iterator is null);

    private static IEnumerable<MemoryFunction> HeapFunctions(TraceModule module)
        => RebaseMemory(ModuleExportReader.Read(module.Path)
                                          .Select(e => MemoryFunction.ClassifyExport(e.Rva, e.Name))
                                          .OfType<MemoryFunction>(),
                        module.StartAddress);

    private static IEnumerable<MemoryFunction> RebaseMemory(IEnumerable<MemoryFunction> functions, ulong address)
        => functions.Select(f => f with { Address = f.Address + address });

    private static (string Pdb, string Guid, int Age) KeyOf(TraceModule module) => (module.Pdb, module.Guid, module.Age);

    private static bool IsReplayed(TraceModule module)
        => module.Pdb.Length > 0 && Modules.Contains(module.Name, StringComparer.OrdinalIgnoreCase);

    private static bool IsHeap(TraceModule module)
        => HeapModules.Contains(Path.GetFileNameWithoutExtension(module.Path), StringComparer.OrdinalIgnoreCase);

    private static Task EnsureResolvedAsync(IEnumerable<TraceModule> modules,
                                            string symbolsPath,
                                            ILogger? logger,
                                            IProgress<ProgressDetail>? progress,
                                            CancellationToken cancellationToken)
        => Task.WhenAll(modules.Where(m => !Cache.ContainsKey(KeyOf(m)))
                               .Select(m => ResolvingOf(m, symbolsPath, logger, progress)))
               .WaitAsync(cancellationToken);

    private static Task ResolvingOf(TraceModule module,
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
                                                      TraceModule module,
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

    private static void Resolve(TraceModule module, string symbolsPath, ILogger? logger)
    {
        var start = Stopwatch.GetTimestamp();

        if (Find(module, symbolsPath) is not { } relative)
        {
            logger?.LogWarning("Replay functions for {Module} not resolved, {Pdb} was not found", module.Name, module.Pdb);

            return;
        }

        Cache[KeyOf(module)] = relative;

        logger?.LogInformation("Replay functions for {Module} resolved from {Pdb} in {Elapsed}: {Excluded} excluded, "
                               + "{Iterators} iterator methods, {Locators} locator reads, {Publishers} event publishers, "
                               + "{Memory} memory functions",
                               module.Name,
                               module.Pdb,
                               Stopwatch.GetElapsedTime(start),
                               relative.Replay.Excluded.Length,
                               relative.Replay.InstanceMethods.Length,
                               relative.Replay.Locators.Length,
                               relative.Replay.Publishers.Length,
                               relative.Memory.Length);
    }

    private static ModuleFunctions? Find(TraceModule module, string symbolsPath)
    {
        var pdbPath = CallstackResolver.GetPdbPath(symbolsPath, module.Frame(module.StartAddress, 0));

        if (!File.Exists(pdbPath))
        {
            return null;
        }

        using var resolver = new DiaResolver(pdbPath);

        var symbols = ModuleSymbols.Load(resolver);

        var mappings = CategoryMappings.Default;

        HashSet<uint> excluded = [.. symbols.FindAddresses(names => names.All(n => IsExcluded(mappings, module.Name, n)))];

        var iterates = string.Equals(module.Name, IteratorModule, StringComparison.OrdinalIgnoreCase);

        InstanceMethod[] instanceMethods = iterates ? [.. InstanceMethodCollector.Find(symbols, resolver)] : [];

        var kept = symbols.Addresses.Where(a => !excluded.Contains(a));

        LocatorRead[] locators = iterates ? [.. LocatorReadResolver.FindReads(resolver, kept)] : [];

        var replay = new ReplayFunctionSet([.. excluded.Select(rva => (ulong)rva)],
                                           instanceMethods,
                                           locators,
                                           [.. symbols.ClassifyStrict((_, name) => EventOf(name)).Select(p => new EventPublisher(p.Rva, p.Value))],
                                           [.. symbols.FindAddresses(names => names.All(IsReserve)).Select(rva => (ulong)rva)]);

        return new ModuleFunctions(replay, [.. symbols.ClassifyStrict((rva, name) => MemoryFunction.Classify(rva, name)).Select(p => p.Value)]);
    }

    private static bool IsExcluded(CategoryMappings mappings, string module, string symbol)
    {
        var (className, methodName) = Split(symbol);

        return IsExcluded(mappings.Classify(module, className, methodName),
                          mappings.ClassifyOperator(module, className, methodName).Iterator);
    }

    private static string? EventOf(string symbol)
    {
        var (className, methodName) = Split(symbol);

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

    private static bool IsReserve(string symbol) => Split(symbol) is (BufferManager, ReserveMethod);

    private static (string? Class, string Method) Split(string symbol)
    {
        var separator = ResolvedCallstackFrameParser.FindClassMethodSeparator(symbol);

        return separator >= 0 ? (symbol[..separator], symbol[(separator + 2)..]) : (null, symbol);
    }

    private sealed record ModuleFunctions(ReplayFunctionSet Replay, MemoryFunction[] Memory)
    {
        public ModuleFunctions Rebase(ulong address) => new(Replay.Rebase(address), [.. RebaseMemory(Memory, address)]);
    }
}
