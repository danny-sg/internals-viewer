using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed record CallStackPlace(EngineEvent? Event, CallStackNode? Node, bool Focus, CallStackNode? Function, int? Call);
