using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;

namespace InternalsViewer.UI.App.Services.Query;

public sealed partial class TraceTickTrackStore : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<TraceTickDefinition> _definitions = [];

    public void Add(TraceTickDefinition definition) => Definitions = [.. Definitions, definition];

    public void AddRange(IEnumerable<TraceTickDefinition> definitions) => Definitions = [.. Definitions, .. definitions];

    public void Replace(TraceTickDefinition existing, TraceTickDefinition definition)
        => Definitions = [.. Definitions.Select(d => ReferenceEquals(d, existing) ? definition : d)];

    public void Remove(TraceTickDefinition definition) => Definitions = [.. Definitions.Where(d => !ReferenceEquals(d, definition))];

    public void Clear() => Definitions = [];
}
