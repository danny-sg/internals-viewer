namespace InternalsViewer.Query.FullTrace.Batches;

public sealed record BatchDeepValue(int Length, byte[] Data)
{
    public bool IsReadable => Length > 0 || Data.Length > 0;

    public bool IsTruncated => Data.Length < Length;
}
