namespace InternalsViewer.Query.FullTrace.Locators;

internal readonly record struct FoundLocator(int CallIndex, int Packed)
{
    public FoundLocator(int callIndex, int poolIndex, bool isReadAtReturn) : this(callIndex, (poolIndex << 1) | (isReadAtReturn ? 1 : 0))
    {
    }

    public int PoolIndex => Packed >> 1;

    public bool IsReadAtReturn => (Packed & 1) != 0;
}
