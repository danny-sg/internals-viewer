using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Page;

namespace InternalsViewer.UI.App.ViewModels.Page;

internal sealed record PageDisplay(Internals.Engine.Pages.Page Page, List<PageSlot> Slots, short? Slot)
{
    public AllocationUnit? AllocationUnit { get; init; }

    public List<IRecord>? Records { get; init; }

    public QueryResultSet? RecordsResultSet { get; init; }

    public AllocationLayer? AllocationLayer { get; init; }

    public short? AllocationFileId { get; init; }

    public int? AllocationStartPage { get; init; }

    public PfsChain? PfsChain { get; init; }

    public bool? IsRowDataTabVisible { get; init; }

    public bool? IsAllocationsTabVisible { get; init; }

    public bool? IsPfsTabVisible { get; init; }

    public (int From, int To)? TabSwitch { get; init; }
}