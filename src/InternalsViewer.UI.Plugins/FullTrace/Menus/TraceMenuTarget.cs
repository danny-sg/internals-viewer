using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.UI.Plugins.FullTrace.Menus.Enums;

namespace InternalsViewer.UI.Plugins.FullTrace.Menus;

public sealed record TraceMenuTarget(TraceMenuSite Site, CallTreeNode? Node, int CallIndex);
