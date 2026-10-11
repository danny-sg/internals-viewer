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
    private const string PackageSeparator = "Pkg::";

    private const string PublishMethod = "Publish";

    private const string BufferManager = "XE_BufferMgr";

    private const string ReserveMethod = "Reserve";

    private static readonly string[] Modules = ["sqlmin", "sqllang", "sqldk"];

    private static readonly string[] HeapModules = ["ntdll", "kernelbase", "kernel32", "ucrtbase"];

    private static ConcurrentDictionary<(string Pdb, string Guid, int Age), ReplayFunctionSet> Cache { get; } = new();

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
                                .Where(m => m.Pdb.Length > 0 && Modules.Contains(m.Name, StringComparer.OrdinalIgnoreCase))
                                .ToList();

        if (identities.Count == 0)
        {
            return new ReplayFunctionSet([], [.. heap], new Dictionary<ulong, string>(), [], [], []);
        }

        var missing = identities.Where(m => !Cache.ContainsKey(KeyOf(m))).ToList();

        if (missing.Count > 0)
        {
            await SymbolDownloader.DownloadSymbols([.. missing.Select(m => m.Frame(m.Address, 0))],
                                                   symbolsPath,
                                                   progress,
                                                   cancellationToken);

            await Task.Run(() => missing.AsParallel()
                                        .WithCancellation(cancellationToken)
                                        .ForAll(m => Resolve(m, symbolsPath, logger)),
                           cancellationToken);
        }

        var found = identities.Select(m => Cache.TryGetValue(KeyOf(m), out var relative) ? relative.Rebase(m.Address) : null)
                              .OfType<ReplayFunctionSet>()
                              .ToList();

        logger?.LogInformation("Replay functions for {Modules} resolved in {Elapsed} ({Cached} of {Count} from the cache)",
                               string.Join(", ", identities.Select(m => m.Name)),
                               Stopwatch.GetElapsedTime(start),
                               identities.Count - missing.Count,
                               identities.Count);

        return new ReplayFunctionSet([.. found.SelectMany(f => f.Excluded)],
                                     [.. found.SelectMany(f => f.Memory), .. heap],
                                     found.SelectMany(f => f.Publishers).ToDictionary(p => p.Key, p => p.Value),
                                     [.. found.SelectMany(f => f.BufferReserves)],
                                     [.. found.SelectMany(f => f.InstanceMethods)],
                                     [.. found.SelectMany(f => f.Locators)]);
    }

    internal static bool IsExcluded(SymbolCategory category)
        => category is SymbolCategory.XEventInfrastructure or SymbolCategory.Tracing;

    private static IEnumerable<MemoryFunction> HeapFunctions(TimeTravelModule module)
        => ModuleExports.Read(module.Path)
                        .Select(e => MemoryFunction.ClassifyExport(module.Address + e.Rva, e.Name))
                        .OfType<MemoryFunction>();

    private static (string Pdb, string Guid, int Age) KeyOf(TimeTravelModuleIdentity module) => (module.Pdb, module.Guid, module.Age);

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
                               + "{Publishers} Extended Events publishers, {Iterators} iterator methods, {Locators} locator reads",
                               module.Name,
                               module.Pdb,
                               Stopwatch.GetElapsedTime(start),
                               relative.Excluded.Length,
                               relative.Publishers.Count,
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

        var publishers = new Dictionary<uint, string?>();

        var reserves = new HashSet<uint>();

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

        locatorFunctions?.ExceptWith(excluded);

        return new ReplayFunctionSet([.. excluded.Select(rva => module.Address + rva)],
                                     [.. memory.Values.OfType<MemoryFunction>()],
                                     publishers.Where(p => p.Value is not null).ToDictionary(p => module.Address + p.Key, p => p.Value!),
                                     [.. reserves.Select(rva => module.Address + rva)],
                                     iteratorMethods is null
                                         ? []
                                         : [.. iteratorMethods.Resolve(resolver).Select(rva => module.Address + rva)],
                                     locatorFunctions is null ? [] : [.. LocatorReads.Find(resolver, module.Address, locatorFunctions)]);
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
