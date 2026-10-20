using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.Controls.Trace;

internal readonly record struct TraceCallLinkHit(int Row, int Track, TraceTickTrack TickTrack, TraceTick Tick);
