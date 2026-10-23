using System;
using InternalsViewer.Query.Results.Enums;

namespace InternalsViewer.UI.App.Controls.Results;

public sealed class ResultSortEventArgs(int ordinal, ResultSortDirection? direction) : EventArgs
{
    public int Ordinal { get; } = ordinal;

    public ResultSortDirection? Direction { get; } = direction;
}
