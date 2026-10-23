using InternalsViewer.UI.App.Models.Query.Trace;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;


namespace InternalsViewer.UI.App.Controls.Trace.Operators;

internal readonly record struct TraceTickHit(int Row, int Track, TraceTickTrack TickTrack, TraceTick? Tick);
