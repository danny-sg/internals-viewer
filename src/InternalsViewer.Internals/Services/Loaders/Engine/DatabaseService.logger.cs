using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Services.Loaders.Engine;

public partial class DatabaseService
{
    [LoggerMessage(LogLevel.Trace, "File Allocations: Refreshing File Id {FileId}")]
    static partial void LogRefreshFileAllocations(ILogger<DatabaseService> logger, short fileId);

    [LoggerMessage(LogLevel.Debug,
                   "Allocation Unit Id: {AllocationUnitId} - Loading from First IAM page: {FirstIamPage}")]
    static partial void LogLoadIamChain(ILogger<DatabaseService> logger,
                                        long allocationUnitId,
                                        PageAddress firstIamPage);

    [LoggerMessage(LogLevel.Trace, "PFS: Refreshing File Id {FileId}")]
    static partial void LogRefreshPfs(ILogger<DatabaseService> logger, short fileId);
}
