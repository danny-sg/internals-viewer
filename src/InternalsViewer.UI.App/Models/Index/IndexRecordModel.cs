using InternalsViewer.Internals.Engine.Address;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalsViewer.UI.App.Models.Index;

public class IndexRecordModel
{
    public int Slot { get; set; }

    public PageAddress DownPagePointer { get; set; } = PageAddress.Empty;

    public RowIdentifier? RowIdentifier { get; set; } = RowIdentifier.Empty;

    public List<IndexRecordFieldModel> Fields { get; set; } = new();
}