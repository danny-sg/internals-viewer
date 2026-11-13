using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.DataPath;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.UI.App.Models.Query.Trace.DataPath;

public sealed class DataPathColumn(DataPathField field)
{
    public DataPathField Field { get; } = field;

    public string Name => Field.Name;

    public SqlTypeInfo? Type => Field.Type;

    public DefinedValueInfo? Definition => Field.Node.IsStatement ? Field.Definition : null;

    public List<DataPathStage> Outputs { get; } = [];

    public string Text { get; set; } = string.Empty;

    public string Value => Definition is null && Outputs is [var output] ? output.Value : Text;

    public bool IsInScope => Definition is null && Outputs is [var output] ? output.IsInScope : Text.Length > 0;

    public double Height => DataPathGraph.HeaderHeight
                            + (Definition is null ? 0 : DataPathGraph.LineHeight)
                            + DataPathGraph.ValueHeight;

    public double X { get; set; }

    public double Y { get; set; }
}
