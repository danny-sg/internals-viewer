using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.UI.App.Models.Index;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed record HeldRowsSnapshot(IReadOnlyList<IndexRecordModel> Models, List<JoinBufferRow> Buffer);
