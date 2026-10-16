namespace InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;

public sealed record DeferredLobValue(IReadOnlyList<DeferredLobLink> Links) : DeferredValue
{
    public override string ToString() => $"LOB {string.Join(", ", Links.Select(l => l.Page))}";
}
