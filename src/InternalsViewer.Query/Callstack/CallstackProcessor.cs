using InternalsViewer.Query.Callstack.Categories;
using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Callstack;

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
/// First symbols are downloaded from the Microsoft symbol server and cached locally. A path is constructed from
/// PDB\Guid+Age\PDB. This path identifies the correct file from the symbols server and is replicated in the local
/// cache.
///
/// Symbols are then resolved using the Debug Interface Access (DIA) API. DIA allows the mapping of the RVA to the
/// function name with the .PDB files.
///
/// To mitigate the need for the DIA SDK to be installed separately on every deployment the redistributable .dll,
/// msdia140.dll is included. Note - msdia140.dll is specifically listed as an allowed redistributable component -
/// see: https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution
///
/// DIA is a COM API and requires the msdia140.dll to be registered. Rather than requiring the user to register the DLL
/// manually we use InternalsViewer.Query.DiaBridge to provide registration-free access to DIA that can be P/Invoke'd
/// from C#. This is a bit of a faff but is the cleanest way to access DIA without quite heavyweight requirements, e.g.
/// installing Build Tools. 
/// </remarks>
internal class CallstackProcessor
{
    public static async Task<string[]> Process(CallStackTree callStack,
                                               string symbolsPath,
                                               IProgress<string>? progress,
                                               CancellationToken cancellationToken)
    {
        var unknown = new HashSet<string>();

        // The tree already merged identical frames, so each is downloaded and resolved once rather than per event.
        var frames = callStack.Nodes().Select(node => node.Frame!).ToList();

        HealUnsymbolisedFrames(frames);

        await SymbolDownloader.DownloadSymbols(frames, symbolsPath, progress, cancellationToken);

        using var resolver = new CallstackResolver(symbolsPath);

        foreach (var frame in frames)
        {
            if (resolver.TryResolve(frame, out var result) && !string.IsNullOrEmpty(result))
            {
                frame.Resolved = ResolvedCallstackFrameParser.Parse(frame.Module, result);

                if (frame.Resolved.ModuleCategory == ModuleCategory.Unknown)
                {
                    unknown.Add(result);
                }

                if (frame.Resolved.SymbolCategory == SymbolCategory.Unknown)
                {
                    unknown.Add(result);
                }
            }
        }

        return unknown.ToArray();
    }

    // SQL sometimes emits a frame without symbol info — no pdb/guid and the address baked into the module name
    // ("sqllang@0x7FFD2D950584") — even though the same address is fully symbolised on another frame. Recover it: each
    // module's load base is address - rva from a symbolised frame, so an address-only frame gets rva = address - base
    // and borrows that module's pdb/guid. It then resolves, and merges with its symbolised twin in the function tree.
    private static void HealUnsymbolisedFrames(List<CallstackFrame> frames)
    {
        var modules = new Dictionary<string, (ulong Base, string Pdb, string Guid, int Age)>(StringComparer.OrdinalIgnoreCase);

        foreach (var frame in frames)
        {
            if (frame.Pdb.Length > 0 && frame.Address >= frame.Rva && !modules.ContainsKey(frame.Module))
            {
                modules[frame.Module] = (frame.Address - frame.Rva, frame.Pdb, frame.Guid, frame.Age);
            }
        }

        foreach (var frame in frames)
        {
            if (frame.Pdb.Length > 0 || frame.Address == 0)
            {
                continue;
            }

            var at = frame.Module.IndexOf('@');

            var module = at >= 0 ? frame.Module[..at] : frame.Module;

            if (modules.TryGetValue(module, out var info) && frame.Address >= info.Base)
            {
                frame.Module = module;
                frame.Pdb = info.Pdb;
                frame.Guid = info.Guid;
                frame.Age = info.Age;
                frame.Rva = (uint)(frame.Address - info.Base);
            }
        }
    }
}