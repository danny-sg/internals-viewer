using InternalsViewer.Query.FullTrace.Timeline.Ticks;


namespace InternalsViewer.UI.App.Controls.Trace;

internal readonly record struct TraceTickHit(int Row, int Track, TraceTickTrack TickTrack, TraceTick? Tick);
