using System;
using InternalsViewer.Query.Results;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Results;

public sealed class ResultHeaderMenuEventArgs(ResultColumn column, MenuFlyout menu) : EventArgs
{
    public ResultColumn Column { get; } = column;

    public MenuFlyout Menu { get; } = menu;
}
