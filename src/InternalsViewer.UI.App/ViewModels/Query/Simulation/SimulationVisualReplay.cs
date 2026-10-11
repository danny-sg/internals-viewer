using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.ViewModels.Query.Simulation;

public sealed record SimulationVisualReplay(List<PageSpan> Visited,
                                       PageAddress? LastPage,
                                       PageAddress? LastDataPage,
                                       int? LastSlot,
                                       int LastSlotCount);
