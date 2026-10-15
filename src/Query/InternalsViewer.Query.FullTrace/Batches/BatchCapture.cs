using InternalsViewer.Query.FullTrace.Batches.Enums;

namespace InternalsViewer.Query.FullTrace.Batches;

public sealed record BatchCapture(ulong Function,
                                  ulong Instance,
                                  int Call,
                                  ulong Id,
                                  ulong Iterator,
                                  ulong Address,
                                  ulong Consumer,
                                  BatchCaptureFlags Flags,
                                  ushort[] Selection,
                                  IReadOnlyList<BatchCaptureVector> Vectors,
                                  int Capacity = 0)
{
    public int SelectedCount => Selection.Length;

    public int RowCount { get; } = Selection.Length == 0 ? 0 : Selection.Max() + 1;
}
