namespace InternalsViewer.Query.FullTrace.Helpers;

internal readonly record struct SweepSpan<TPosition>(uint Thread, TPosition From, TPosition To, ulong Instance);
