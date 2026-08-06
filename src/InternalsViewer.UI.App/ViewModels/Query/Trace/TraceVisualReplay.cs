using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed record TraceVisualReplay(List<PageSpan> Visited,
                                       PageAddress? LastPage,
                                       PageAddress? LastDataPage,
                                       int? LastSlot,
                                       int LastSlotCount);
