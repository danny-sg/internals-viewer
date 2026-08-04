using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Models.Index;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceRowStreamViewModel(string name) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private ObservableCollection<IndexRecordModel> _rows = [];

    public string Name { get; } = name;

    public string Title => Rows.Count > 0 ? $"{Name} ({Rows.Count:N0})" : Name;

    public void Add(IndexRecordModel row)
    {
        Rows.Add(row);

        OnPropertyChanged(nameof(Title));
    }

    public void Replace(IEnumerable<IndexRecordModel> rows)
    {
        Rows = new ObservableCollection<IndexRecordModel>(rows);
    }

    public void Clear()
    {
        Rows.Clear();

        OnPropertyChanged(nameof(Title));
    }
}
