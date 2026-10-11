using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.ViewModels.Query.Simulation;

public sealed record SimulationPositionUpdate(IReadOnlyDictionary<int, (PageAddress? Page, int? Slot)> Positions,
                                         IReadOnlySet<int> Open);
