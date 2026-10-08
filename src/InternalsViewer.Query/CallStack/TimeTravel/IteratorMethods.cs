using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;
using InternalsViewer.Query.CallStack.Symbols;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public static class IteratorMethods
{
    private const string Module = "sqlmin";

    private static readonly string[] ClassPrefixes = ["CQScan", "CBpQScan", "CQueryScan"];

    private static readonly HashSet<string> Methods = new(StringComparer.Ordinal)
    {
        "Open",
        "GetRow",
        "GetRowOrReQualifyHelper",
        "Close",
        "BpOpen",
        "BpGetNextBatch",
        "BpClose"
    };

    public static async Task<ulong[]> ResolveAsync(IReadOnlyList<TimeTravelModule> modules,
                                                   string symbolsPath,
                                                   IProgress<string>? progress,
                                                   CancellationToken cancellationToken)
    {
        var module = modules.FirstOrDefault(m => string.Equals(Path.GetFileNameWithoutExtension(m.Path),
                                                               Module,
                                                               StringComparison.OrdinalIgnoreCase));

        if (module is null)
        {
            return [];
        }

        var frame = TimeTravelModuleIdentity.Describe(module).Frame(module.Address, 0);

        if (frame.Pdb.Length == 0)
        {
            return [];
        }

        await SymbolDownloader.DownloadSymbols([frame], symbolsPath, progress, cancellationToken);

        var pdbPath = CallstackResolver.GetPdbPath(symbolsPath, frame);

        if (!File.Exists(pdbPath))
        {
            return [];
        }

        var rvas = await Task.Run(() => Find(pdbPath), cancellationToken);

        return [.. rvas.Select(rva => module.Address + rva)];
    }

    internal static bool TakesIterator(string? signature, string? decoratedName)
    {
        if (signature is null || decoratedName is null)
        {
            return true;
        }

        return FunctionSignature.Parse(signature, decoratedName) switch
        {
            { Kind: FunctionKind.Member } => true,
            { Kind: FunctionKind.Static, Parameters: [var first, ..] } => IsIteratorClass(first) && first.TrimEnd().EndsWith('*'),
            _ => false
        };
    }

    internal static bool IsIteratorMethod(string symbol)
    {
        var separator = ResolvedCallstackFrameParser.FindClassMethodSeparator(symbol);

        return separator > 0
               && IsIteratorClass(symbol)
               && Methods.Contains(symbol[(separator + 2)..]);
    }

    private static List<uint> Find(string pdbPath)
    {
        using var resolver = new DiaResolver(pdbPath);

        var methods = new HashSet<uint>();

        var shared = new HashSet<uint>();

        foreach (var symbol in resolver.EnumerateSymbolDetails(string.Empty, includeSignature: false))
        {
            if (!symbol.IsFunction)
            {
                continue;
            }

            if (IsIteratorMethod(symbol.Name))
            {
                methods.Add(symbol.Rva);
            }
            else if (!IsIteratorClass(symbol.Name))
            {
                shared.Add(symbol.Rva);
            }
        }

        methods.ExceptWith(shared);

        return [.. methods.Where(rva => TakesIterator(Signature(resolver, rva), resolver.GetDecoratedName(rva)))];
    }

    private static string? Signature(DiaResolver resolver, uint rva)
        => resolver.EnumerateSymbolsAtRva(rva)
                   .Where(s => s.IsFunction && s.Signature.Length > 0)
                   .Select(s => s.Signature)
                   .FirstOrDefault();

    private static bool IsIteratorClass(string symbol)
        => ClassPrefixes.Any(prefix => symbol.StartsWith(prefix, StringComparison.Ordinal));
}
