using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.CallStack.Tree;

public interface ICallTreeState
{
    IReadOnlyList<CallTreeRow> ExpandedRows { get; }

    CallTreeRow? SelectedRow { get; }
}
