using InternalsViewer.Query.FullTrace.Memory.History.Batches.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.History.Batches;

public readonly record struct BatchPart(BatchPartType Type, int VectorId = 0)
{
    public string Title => Type switch
    {
        BatchPartType.Header
            => "Batch",
        BatchPartType.VectorArray
            => "Vector Array",
        BatchPartType.AllocationBitmap
            => "Allocation Bitmap",
        BatchPartType.PureBitmap
            => "Pure Bitmap",
        BatchPartType.Selection
            => "Selection Vector",
        BatchPartType.DeepData
            => "Deep Data",
        _ => "Vector"
    };

    public string Name => Type switch
    {
        BatchPartType.Header
            => BatchReader.ClassName,
        BatchPartType.Vector
            => $"{BatchReader.ClassName} {Title} {VectorId}",
        _ => $"{BatchReader.ClassName} {Title}"
    };
}
