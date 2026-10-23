using System;
using InternalsViewer.Query.Results;

namespace InternalsViewer.UI.App.Controls.Results;

public sealed class ResultCellHoverEventArgs(ResultRow<long> row, int ordinal, object? value, bool isOver) : EventArgs
{
    public ResultRow<long> Row { get; } = row;

    public int Ordinal { get; } = ordinal;

    public object? Value { get; } = value;

    public bool IsOver { get; } = isOver;
}
