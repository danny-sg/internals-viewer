using InternalsViewer.Query.CallStack;
using InternalsViewer.UI.Plugins.FullTrace.Menus.Enums;

namespace InternalsViewer.UI.Plugins.FullTrace.Menus;

public sealed record TraceMenuTarget(TraceMenuSite Site, CallStackNode? Node, int CallIndex);
