using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.Models.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceRowStreamViewModel : ObservableObject
{
    public BulkObservableCollection<IndexRecordModel> Rows { get; } = [];

    public bool IsAccumulating { get; set; }

    public void Show(IndexRecordModel row)
    {
        if (!IsAccumulating)
        {
            Rows.Clear();
        }

        Rows.Add(row);
    }

    public void Load(IReadOnlyList<IndexRecordModel> rows)
    {
        Rows.Reset(rows);
    }

    public void Clear()
    {
        Rows.Clear();
    }
}
