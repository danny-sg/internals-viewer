using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.UI.App.Models.Index;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceHeldRowsViewModel : ObservableObject
{
    public ObservableCollection<IndexRecordModel> Rows { get; } = [];

    private List<JoinBufferRow> _synced = [];

    public void Sync(IReadOnlyList<JoinBufferRow> buffer)
    {
        if (_synced.SequenceEqual(buffer))
        {
            return;
        }

        _synced = [.. buffer];

        Refill(buffer);
    }

    public void Reset()
    {
        _synced = [];

        Rows.Clear();
    }

    /// <summary>
    /// Rebuilds a row pane from the buffer, in the collection the grid is already bound to
    /// </summary>
    /// <remarks>
    /// Handing the grid a new collection rebinds it, which rebuilds its columns and every row container it had realised - the whole pane
    /// rebuilt for a step that moved one row, paid on every step of the walk. Refilling the collection it holds costs none of that. The
    /// rows are rebuilt rather than the changed ones picked out because the grid's collection view does not track an item replaced in
    /// place, and a buffer holds only what a join is working with, which is a row or two.
    /// </remarks>
    private void Refill(IReadOnlyList<JoinBufferRow> buffer)
    {
        Rows.Clear();

        foreach (var row in buffer)
        {
            var model = TraceVisualViewModel.ToRecordModel(row.Record);

            model.IsMatched = row.IsMatched;

            Rows.Add(model);
        }
    }
}
