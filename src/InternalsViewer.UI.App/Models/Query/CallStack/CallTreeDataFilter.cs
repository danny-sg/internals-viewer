using System;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

[Flags]
public enum CallTreeDataFilter
{
    None = 0,
    Batches = 1,
    Rows = 2
}
