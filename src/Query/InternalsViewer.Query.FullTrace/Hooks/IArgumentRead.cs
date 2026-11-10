namespace InternalsViewer.Query.FullTrace.Hooks;

internal interface IArgumentRead
{
    int ArgumentSlot { get; }

    bool IsReadAtReturn { get; }
}
