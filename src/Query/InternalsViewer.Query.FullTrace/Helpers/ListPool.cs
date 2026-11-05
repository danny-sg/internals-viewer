namespace InternalsViewer.Query.FullTrace.Helpers;

internal sealed class ListPool<T>
{
    private const int MaximumCapacity = 1024;

    private Stack<List<T>> Free { get; } = [];

    public List<T> Rent() => Free.TryPop(out var list) ? list : [];

    public void Return(List<T> list)
    {
        if (list.Capacity > MaximumCapacity)
        {
            return;
        }

        list.Clear();

        Free.Push(list);
    }
}
