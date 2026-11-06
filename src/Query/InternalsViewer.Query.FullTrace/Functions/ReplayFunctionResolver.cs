using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.CallStack;
using System.Collections.Concurrent;
using System.Diagnostics;
using InternalsViewer.Internals.Engine.Loading;
using InternalsViewer.Query.CallStack.Symbols;
using Microsoft.Extensions.Logging;
using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace.Functions;

/// <summary>
/// Resolves a ReplayFunctions for the trace
/// </summary>
/// <remarks>
/// ReplayFunctions provides functions per module:
/// 
///     - Excluded from the call stack
///     - Memory functions for allocations/de-allocations
///     - Instance methods
///     - Locator reads
///     - Memory map functions
///     - Plan node functions
///     - Output functions
///     - Vtables
///
/// Results are prepared and cached per module as the derivation of the function sets is expensive, but won't change from run to run.
///
/// To make decisions about the category of a function debug symbols must translate from a module + RVA to a function name. This is done by
/// downloading the symbols and using the DIA bridge for name resolution.
///
/// Exclusions
///
///     Excluded functions based on mapped category using CategoryMappings: XEventInfrastructure always, Tracing unless the function
///     belongs to an operator.
///
/// Memory Functions
///
///     Memory allocation and deallocations functions.
///
///     SQL Server memory functions are identified by name, from the allocator functions and allocator class prefixes that
///     AllocationFunction.Classify knows. AllocationSiteResolver adds the file and line argument slots of the allocators that take them.
///
///     Windows heap + C Runtime memory function names are resolved using ModuleExportReader that reads the PE export table to get the name
///     from an address and classifies the function via AllocationFunction.ClassifyExport. The reason this is done instead of downloading
///     symbols is that first the data is available directly, and second a Windows Update could bump the age of the module requiring
///     frequent downloads of new files.
///
/// Instance Methods
///
///     Functions keyed from a class instance pointer. These are identified via InstanceMethodCollector from the iterator, query scan and
///     plan object class names in IteratorSymbols, with the DIA signature deciding the register: RCX for a member function, the keyed
///     pointer parameter for a static one.
///
/// Locators
///
///     Functions whose arguments or return value point at known structures, such as pages, rows and variants, picked by
///     LocatorReadResolver from the parsed signatures.
///
/// Address Map Functions
///
///     Constructors and member functions whose instance pointer tells the memory map hook what lives at an address, and the pointer
///     parameters and returns declared with a class, found by AddressMapFunctionResolver.
///
/// Plan Node Functions
///
///     The iterator, batch helper, batch factory, constructor and teardown functions the plan node hook watches, with the plan object
///     vtables, found by PlanNodeFunctions.Collector.
///
/// Output Functions
///
///     The expression evaluation, value read, variant read and send functions the output hook watches to find where each output column
///     reads from, found by OutputFunctions.Collector.
///
/// Vtables
///
///     Class names by vtable address from the module's data symbols, found by VtableResolver, so a pointer can be typed from the vtable
///     it points at.
/// </remarks>
internal static class ReplayFunctionResolver
{
    private static readonly string[] Modules = 
    [
        // Relational + Storage Engine
        "sqlmin", 
        // Query processor + optimizer
        "sqllang",
        // SQL OS scheduler, memory management
        "sqldk",
        // Transaction Services
        "sqltses"
    ];

    private static readonly string[] HeapModules = ["ntdll", "kernelbase", "kernel32", "ucrtbase"];

    private static ConcurrentDictionary<(string Pdb, string Guid, int Age), ReplayFunctionTable> Cache { get; } = new();

    private static ConcurrentDictionary<(string Pdb, string Guid, int Age), Lazy<Task>> Resolving { get; } = new();

    public static Task PrepareAsync(IEnumerable<string> modulePaths,
                                    string symbolsPath,
                                    ILogger? logger,
                                    CancellationToken cancellationToken)
    {
        return EnsureResolvedAsync([.. modulePaths.Select(p => TraceModule.Read(p, 0, 0)).Where(IsReplayed)],
                                   symbolsPath,
                                   logger,
                                   null,
                                   cancellationToken);
    }

    public static async Task<ReplayFunctionTable> ResolveAsync(IReadOnlyList<TraceModule> modules,
                                                               string symbolsPath,
                                                               ILogger? logger,
                                                               IProgress<ProgressDetail>? progress,
                                                               CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();

        List<(ReplayFunctionTable, ulong)> heap = [.. modules.Where(IsHeap).Select(m => (GetHeapFunctions(m), m.StartAddress))];

        var identities = modules.Where(IsReplayed).ToList();

        if (identities.Count == 0)
        {
            return ReplayFunctionTable.Merge(heap);
        }

        var missing = identities.Count(m => !Cache.ContainsKey(GetKey(m)));

        await EnsureResolvedAsync(identities, symbolsPath, logger, progress, cancellationToken);

        List<(ReplayFunctionTable, ulong)> found = [];

        foreach (var module in identities)
        {
            if (Cache.TryGetValue(GetKey(module), out var relative))
            {
                found.Add((relative, module.StartAddress));
            }
        }

        logger?.LogInformation("Replay functions for {Modules} resolved in {Elapsed} ({Cached} of {Count} from the cache)",
                               string.Join(", ", identities.Select(m => m.Name)),
                               Stopwatch.GetElapsedTime(start),
                               identities.Count - missing,
                               identities.Count);

        return ReplayFunctionTable.Merge([.. found, .. heap]);
    }

    private static ReplayFunctionTable GetHeapFunctions(TraceModule module)
    {
        AllocationFunction[] memory = [.. ModuleExportReader.Read(module.Path)
                                                            .Select(e => AllocationFunction.ClassifyExport(e.Rva, e.Name))
                                                            .OfType<AllocationFunction>()];

        return ReplayFunctionTable.Empty with { AllocationFunctions = memory };
    }

    private static (string Pdb, string Guid, int Age) GetKey(TraceModule module) => (module.Identity.Pdb, module.Identity.Guid, module.Identity.Age);

    private static bool IsReplayed(TraceModule module)
        => module.Identity.Pdb.Length > 0 && Modules.Contains(module.Name, StringComparer.OrdinalIgnoreCase);

    private static bool IsHeap(TraceModule module)
        => HeapModules.Contains(Path.GetFileNameWithoutExtension(module.Path), StringComparer.OrdinalIgnoreCase);

    private static Task EnsureResolvedAsync(IEnumerable<TraceModule> modules,
                                            string symbolsPath,
                                            ILogger? logger,
                                            IProgress<ProgressDetail>? progress,
                                            CancellationToken cancellationToken)
    {
        return Task.WhenAll(modules.Where(m => !Cache.ContainsKey(GetKey(m)))
                                   .Select(m => GetOrResolve(m, symbolsPath, logger, progress)))
                   .WaitAsync(cancellationToken);
    }

    private static Task GetOrResolve(TraceModule module,
                                     string symbolsPath,
                                     ILogger? logger,
                                     IProgress<ProgressDetail>? progress)
        => Resolving.GetOrAdd(GetKey(module),
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
            await SymbolDownloader.DownloadSymbols([module.Identity], symbolsPath, progress);

            ResolveModule(module, symbolsPath, logger);
        }
        finally
        {
            Resolving.TryRemove(key, out _);
        }
    }

    private static void ResolveModule(TraceModule module, string symbolsPath, ILogger? logger)
    {
        var start = Stopwatch.GetTimestamp();

        if (GetFunctions(module, symbolsPath) is not { } relative)
        {
            logger?.LogWarning("Replay functions for {Module} not resolved, {Pdb} was not found", module.Name, module.Identity.Pdb);

            return;
        }

        Cache[GetKey(module)] = relative;

        logger?.LogInformation("Replay functions for {Module} resolved from {Pdb} in {Elapsed}: {Excluded} excluded, "
                               + "{Iterators} iterator methods, {Locators} locator reads, "
                               + "{Memory} memory functions, {Members} member functions, {Vtables} vtables",
                               module.Name,
                               module.Identity.Pdb,
                               Stopwatch.GetElapsedTime(start),
                               relative.ExcludedFunctions.Length,
                               relative.InstanceMethods.Length,
                               relative.LocatorReads.Length,
                               relative.AllocationFunctions.Length,
                               relative.AddressMapFunctions.Length,
                               relative.Vtables.Count);
    }

    private static ReplayFunctionTable? GetFunctions(TraceModule module, string symbolsPath)
        => SymbolTableCache.Get(symbolsPath, module.Identity) is { } table ? ReplayFunctionScan.Scan(table, module.Name) : null;
}
