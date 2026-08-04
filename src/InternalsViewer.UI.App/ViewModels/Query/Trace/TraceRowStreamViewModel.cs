using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Models.Index;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceRowStreamViewModel : ObservableObject
{
    public ObservableCollection<IndexRecordModel> Rows { get; } = [];

    public void Show(IndexRecordModel row)
    {
        Rows.Clear();

        Rows.Add(row);
    }

    public void Clear()
    {
        Rows.Clear();
    }
}
