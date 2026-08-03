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

namespace InternalsViewer.Execution.Services.Joins.Inputs;

/// <summary>
/// An input that fetches a heap row using the row identifier carried by the outer row, as a RID lookup does
/// </summary>
/// <remarks>
/// The nonclustered index of a heap stores a row identifier in place of the clustered key, so the outer row already names the page and
/// slot and nothing has to be searched for.
/// </remarks>
public sealed class RidLookupJoinInput(HeapFetchStepService service, AccessPredicate? residual = null) : RebindableJoinInput
{
    public override IStepService Service => service;

    public override AccessStrategy? Strategy => service.Strategy;
    
    public override bool FetchesDirectly => true;

    public override async Task<AccessStep> RebindAsync(DatabaseSource database,
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
