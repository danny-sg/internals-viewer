namespace InternalsViewer.Internals.Helpers;

public class History<T>
{
    private Stack<T> BackStack { get; } = new();

    private Stack<T> ForwardStack { get; } = new();

    public void Add(T item)
    {
        switch (BackStack.Count)
        {
            case > 0 when BackStack.Peek()!.Equals(item):
                return;
            case > 0:
                ForwardStack.Clear();
                break;
        }

        BackStack.Push(item);
    }

    public T? Back()
    {
        if (BackStack.Count > 1)
        {
            ForwardStack.Push(BackStack.Pop());

            return BackStack.Peek();
        }

        return default;
    }

    public T? Forward()
    {
        if (ForwardStack.Count > 0)
        {
            var item = ForwardStack.Pop();

            BackStack.Push(item);

            return item;
        }

        return default;
    }

    public bool CanGoBack() => BackStack.Count > 1;   

    public bool CanGoForward() => ForwardStack.Count > 0;
}
