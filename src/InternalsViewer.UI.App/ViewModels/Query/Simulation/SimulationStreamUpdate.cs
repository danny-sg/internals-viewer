using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Index;

namespace InternalsViewer.UI.App.ViewModels.Query.Simulation;

public sealed record SimulationStreamUpdate(IReadOnlyDictionary<int, IndexRecordModel> LastRows,
                                       IReadOnlyDictionary<int, List<IndexRecordModel>> Accumulated);
