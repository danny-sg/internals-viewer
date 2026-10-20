using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Evaluations;

internal readonly record struct EvaluationCapture(ulong Function, int Call, TraceEvaluationResult Result);
