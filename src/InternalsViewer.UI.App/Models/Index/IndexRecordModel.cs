using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.Models.Index;

public sealed class IndexRecordModel
{
    public int Slot { get; set; }

    public PageAddress DownPagePointer { get; set; } = PageAddress.Empty;

    public RowIdentifier? RowIdentifier { get; set; } = RowIdentifier.Empty;

    public List<IndexRecordFieldModel> Fields { get; set; } = [];

    /// <summary>
    /// Indicates a join has matched this row against the other side
    /// </summary>
    public bool IsMatched { get; set; }
}