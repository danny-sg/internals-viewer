using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;


namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Operators;

internal readonly record struct TraceTickHit(int Row, int Track, TraceTickTrack TickTrack, TraceTick? Tick);
