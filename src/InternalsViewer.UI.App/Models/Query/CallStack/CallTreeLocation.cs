using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed record CallTreeLocation(EngineEvent? Event, CallStackNode? Node, bool Focus, CallStackNode? Function, int? Call);
