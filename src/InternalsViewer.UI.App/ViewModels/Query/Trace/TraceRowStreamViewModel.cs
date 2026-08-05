using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Models.Index;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceRowStreamViewModel : ObservableObject
{
    public ObservableCollection<IndexRecordModel> Rows { get; } = [];

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
        Rows.Clear();

        foreach (var row in rows)
        {
            Rows.Add(row);
        }
    }

    public void Clear()
    {
        Rows.Clear();
    }
}
