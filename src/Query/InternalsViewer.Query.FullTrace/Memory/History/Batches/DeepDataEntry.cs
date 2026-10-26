namespace InternalsViewer.Query.FullTrace.Memory.History.Batches;

public sealed record DeepDataEntry(ulong Address, int Length, byte[] Data)
{
    public const int HeaderSize = 2 * sizeof(ushort);

    public ulong DataAddress => Address + HeaderSize;

    public int Size => HeaderSize + Length;
}
