using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.Trace.CallStack.Tree;

public sealed record CallTreeLayout(IReadOnlyList<CallTreeRow> Roots,
                                    IReadOnlyList<CallTreeRow> Expanded,
                                    bool ExpandRoots,
                                    CallTreeRow? Selected,
                                    object? FocusCurrent,
                                    OperatorLink? FocusParent,
                                    object? FilterCurrent)
{
    public static CallTreeLayout Empty { get; } = new([], [], false, null, null, null, null);
}
