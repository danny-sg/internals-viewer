namespace InternalsViewer.Query.FullTrace.Batches;

public sealed record BatchSummary(int Call,
                                  ulong Iterator,
                                  BatchCaptureFlags Flags,
                                  int SelectedCount,
                                  int RowCount,
                                  int VectorCount,
                                  int PureCount,
                                  int? InputSelectedCount = null);
