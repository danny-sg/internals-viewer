using System;
using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.Trace.DataPath;

public sealed class DataPathGraph(IReadOnlyList<DataPathStage> stages,
                                  IReadOnlyList<DataPathColumn> columns,
                                  IReadOnlyList<string> warnings,
                                  Action<ulong> open)
{
    public const double BoxWidth = 220;

    public const double ColumnWidth = 160;

    public const double OperatorHeight = 28;

    public const double LineHeight = 20;

    public const double BoxHeight = OperatorHeight + LineHeight * 2 + 6;

    public const double ColumnGap = 56;

    public event EventHandler? Changed;

    public IReadOnlyList<DataPathStage> Stages { get; } = stages;

    public IReadOnlyList<DataPathColumn> Columns { get; } = columns;

    public IReadOnlyList<string> Warnings { get; } = warnings;

    public double Width { get; init; }

    public double Height { get; init; }

    public int Version { get; set; }

    public void Open(ulong address) => open(address);

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
