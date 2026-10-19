using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.Controls.Trace;

internal sealed record TraceOperatorRow(ExecutionOperatorEvent Operator,
                                        TraceOperatorLifetime[] Lifetimes,
                                        string Name,
                                        string Target,
                                        string Title);
