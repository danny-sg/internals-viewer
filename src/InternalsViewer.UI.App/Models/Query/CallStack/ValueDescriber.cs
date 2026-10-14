using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public delegate ValueDescription? ValueDescriber(ArgumentSlot slot, ulong value, TraceArgumentCall call);
