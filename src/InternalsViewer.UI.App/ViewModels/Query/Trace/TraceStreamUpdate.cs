using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Index;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed record TraceStreamUpdate(IReadOnlyDictionary<int, IndexRecordModel> LastRows,
                                       IReadOnlyDictionary<int, List<IndexRecordModel>> Accumulated);
