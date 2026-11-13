using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.UI.App.Models.Query.Trace.DataPath;

public sealed class DataPathColumn(string name,
                                   SqlTypeInfo? type,
                                   IReadOnlyList<DataPathInput> inputs,
                                   PlanNode? expression = null,
                                   DefinedValueInfo? definition = null)
{
    public string Name { get; } = name;

    public SqlTypeInfo? Type { get; } = type;

    public IReadOnlyList<DataPathInput> Inputs { get; } = inputs;

    public PlanNode? Expression { get; } = expression;

    public DefinedValueInfo? Definition { get; } = definition;

    public List<DataPathStage> Outputs { get; } = [];

    public string Text { get; set; } = string.Empty;

    public string Value => Expression is null && Outputs is [var output] ? output.Value : Text;

    public bool IsInScope => Expression is null && Outputs is [var output] ? output.IsInScope : Text.Length > 0;

    public double Height => DataPathGraph.HeaderHeight
                            + (Definition is null ? 0 : DataPathGraph.LineHeight)
                            + DataPathGraph.ValueHeight;

    public double X { get; set; }

    public double Y { get; set; }
}
