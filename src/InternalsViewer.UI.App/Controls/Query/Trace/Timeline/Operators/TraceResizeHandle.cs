using InternalsViewer.UI.App.Models.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Operators;

internal readonly record struct TraceResizeHandle(int Row, TraceTickTrack? Track, float StartHeight, float MaximumHeight);
