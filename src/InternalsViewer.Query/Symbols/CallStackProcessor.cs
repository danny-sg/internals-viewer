using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Symbols;

/// <summary>
/// Process the call stack by using the debugging symbols to resolve function names
/// </summary>
/// <remarks>
/// This is a pretty complicated process that requires an additional C++ DLL to integrate the symbols
///
/// The SQL Server call stack provides:
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
internal class CallStackProcessor
{
    public static async Task Process(List<EngineEvent> events, CancellationToken cancellationToken)
    {
        var symbolsPath = @"C:\Symbols";

        await SymbolDownloader.DownloadSymbols(events.SelectMany(e => e.Callstack), symbolsPath, cancellationToken);

        var resolver = new CallStackResolver(symbolsPath);

        foreach (var engineEvent in events)
        {
            if (engineEvent.Callstack.Count > 0)
            {
                foreach (var frame in engineEvent.Callstack)
                {
                    frame.ResolvedSymbol = resolver.Resolve(frame);
                }
            }
        }
    }
}