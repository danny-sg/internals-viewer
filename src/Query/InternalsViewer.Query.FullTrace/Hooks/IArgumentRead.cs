namespace InternalsViewer.Query.FullTrace.Hooks;

internal interface IArgumentRead
{
    int Register { get; }

    bool IsReadAtReturn { get; }
}
