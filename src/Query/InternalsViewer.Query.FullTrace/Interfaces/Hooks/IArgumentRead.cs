namespace InternalsViewer.Query.FullTrace.Interfaces.Hooks;

internal interface IArgumentRead
{
    int Register { get; }

    bool IsReadAtReturn { get; }
}
