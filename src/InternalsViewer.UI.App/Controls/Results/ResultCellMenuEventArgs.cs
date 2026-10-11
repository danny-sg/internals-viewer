using System;
using InternalsViewer.Query.Results;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Results;

public sealed class ResultCellMenuEventArgs(ResultRow<long> row, int ordinal, MenuFlyout menu) : EventArgs
{
    public ResultRow<long> Row { get; } = row;

    public int Ordinal { get; } = ordinal;

    public MenuFlyout Menu { get; } = menu;
}
