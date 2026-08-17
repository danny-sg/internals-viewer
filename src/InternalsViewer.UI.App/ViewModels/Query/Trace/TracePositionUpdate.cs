using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed record TracePositionUpdate(IReadOnlyDictionary<int, (PageAddress? Page, int? Slot)> Positions,
                                         IReadOnlySet<int> Open);
