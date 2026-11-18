using InternalsViewer.Query.FullTrace.DataPath;
using InternalsViewer.Query.FullTrace.DataPath.Enums;
using InternalsViewer.UI.App.Helpers;

namespace InternalsViewer.UI.App.Models.Query.Trace.DataPath;

public sealed class DataPathStage(DataPathField field, string source, ulong sourceAddress, DataPathStage? consumer, DataPathLink? link)
{
    public DataPathField Field { get; } = field;

    public string Source { get; } = source;

    public ulong SourceAddress { get; } = sourceAddress;

    public DataPathStage? Consumer { get; } = consumer;

    public DataPathLink? Link { get; } = link;

    public string Name => Field.Name;

    public string Kind => PlanOutputLinkText.GetReferenceName(Field, Link);

    public string Uses => DataPathText.GetUses(Field.Uses);

    public string Detail { get; init; } = string.Empty;

    public bool IsPointer => Field.FieldType == DataPathFieldType.Pointer;

    public bool IsComputed => Field.FieldType == DataPathFieldType.Computed;

    public bool IsBuild { get; init; }

    public bool IsSeen => Link?.IsSeen ?? true;

    public string Value { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsInScope => Value.Length > 0 && IsActive;

    public double X { get; set; }

    public double Y { get; set; }
}
