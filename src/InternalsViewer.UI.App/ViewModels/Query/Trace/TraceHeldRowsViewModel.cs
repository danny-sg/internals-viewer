using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.Models.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceHeldRowsViewModel : ObservableObject
{
    public BulkObservableCollection<IndexRecordModel> Rows { get; } = [];

    private List<JoinBufferRow> _synced = [];

    public void Sync(IReadOnlyList<JoinBufferRow> buffer)
    {
        if (!HasChanged(buffer))
        {
            return;
        }

        Apply(Capture(buffer));
    }

    public bool HasChanged(IReadOnlyList<JoinBufferRow> buffer)
    {
        return !_synced.SequenceEqual(buffer);
    }

    public void Apply(HeldRowsSnapshot snapshot)
    {
        _synced = snapshot.Buffer;

        Rows.Reset(snapshot.Models);
    }

    public void Reset()
    {
        _synced = [];

        Rows.Reset([]);
    }

    /// <summary>
    /// Rebuilds a row pane from the buffer, in the collection the grid is already bound to
    /// </summary>
    /// <remarks>
    /// Handing the grid a new collection rebinds it, which rebuilds its columns and every row container it had realised - the whole pane
    /// rebuilt for a step that moved one row, paid on every step of the walk. Refilling the collection it holds costs none of that. The
    /// rows are rebuilt rather than the changed ones picked out because the grid's collection view does not track an item replaced in
    /// place.
    ///
    /// A join holds a row or two, but a sort's buffer is its whole sort table, so the refill is done in one go rather than a row at a
    /// time - the grid follows a single change instead of one per collected row.
    /// </remarks>
    public static HeldRowsSnapshot Capture(IReadOnlyList<JoinBufferRow> buffer)
    {
        var models = new List<IndexRecordModel>(buffer.Count);

        foreach (var row in buffer)
        {
            var model = TraceVisualViewModel.ToRecordModel(row.Record);

            model.IsMatched = row.IsMatched;

            models.Add(model);
        }

        return new HeldRowsSnapshot(models, [.. buffer]);
    }
}
