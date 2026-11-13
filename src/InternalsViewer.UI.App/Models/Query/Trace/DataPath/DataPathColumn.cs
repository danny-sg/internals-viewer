using System.Collections.Generic;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.UI.App.Models.Query.Trace.DataPath;

public sealed class DataPathColumn(string name,
                                   string type,
                                   IReadOnlyList<DataPathInput> inputs,
                                   PlanNode? expression = null,
                                   DefinedValueInfo? value = null)
{
    public string Name { get; } = name;

    public string Type { get; } = type;

    public IReadOnlyList<DataPathInput> Inputs { get; } = inputs;

    public PlanNode? Expression { get; } = expression;

    public DefinedValueInfo? Value { get; } = value;

    public double X { get; set; }

    public double Y { get; set; }

    public DataPathStage? Output { get; set; }
}
