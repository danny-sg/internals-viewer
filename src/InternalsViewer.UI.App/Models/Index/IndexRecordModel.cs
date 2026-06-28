using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.Models.Index;

public class IndexRecordModel
{
    public int Slot { get; set; }

    public PageAddress DownPagePointer { get; set; } = PageAddress.Empty;

    public RowIdentifier? RowIdentifier { get; set; } = RowIdentifier.Empty;

    public List<IndexRecordFieldModel> Fields { get; set; } = [];
}