using InternalsViewer.UI.App.Models.Query.Trace;

namespace InternalsViewer.UI.App.Controls.Trace.Operators;

internal readonly record struct TraceResizeHandle(int Row, TraceTickTrack? Track, float StartHeight, float MaximumHeight);
