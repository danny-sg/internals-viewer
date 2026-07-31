using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Services.Heaps;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Services.Joins;

/// <summary>
/// An inner side that fetches a heap row using the row identifier carried by the outer row, as a RID lookup does
/// </summary>
/// <remarks>
/// The nonclustered index of a heap stores a row identifier in place of the clustered key, so the outer row already names the page and
/// slot and nothing has to be searched for.
/// </remarks>
public sealed class RidLookupInnerSide(HeapFetchStepService service, AccessPredicate? residual = null) : ILoopInnerSide
{
    public IStepService Service => service;

    public AccessStrategy? Strategy => service.Strategy;

    public string StartDescription => "on the row identifier each index row carries. Each outer row names a heap page and slot outright";

    public bool FetchesDirectly => true;

    public async Task<AccessStep> RebindAsync(DatabaseSource database,
                                              IRecord outerRecord,
                                              int rebindNumber,
                                              CancellationToken cancellationToken)
    {
        var rowIdentifier = GetRowIdentifier(outerRecord);

        await service.StartAsync(database, rowIdentifier, residual, cancellationToken);

        return new AccessStep.Rebind(rebindNumber, default) { RowIdentifier = rowIdentifier };
    }

    private static RowIdentifier GetRowIdentifier(IRecord outerRecord)
    {
        if (outerRecord is FixedVarIndexRecord { Rid: { } rid })
        {
            return rid;
        }

        if (outerRecord is CdIndexRecord { Rid: { } compressedRid })
        {
            return compressedRid;
        }

        throw new InvalidOperationException("The outer row carries no row identifier, so it cannot drive a RID lookup");
    }
}
