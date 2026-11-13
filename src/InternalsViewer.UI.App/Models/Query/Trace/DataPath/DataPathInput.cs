using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.UI.App.Models.Query.Trace.DataPath;

public readonly record struct DataPathInput(int NodeId, PlanOutputColumn Column);
