using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.CallStack.Tree;

public sealed class CallTreeRow(object content)
{
    public object Content { get; } = content;

    public List<CallTreeRow> Children { get; } = [];

    public CallTreeRow? Parent { get; private set; }

    public bool IsRevealed { get; init; }

    public static IEnumerable<CallTreeRow> Walk(IEnumerable<CallTreeRow> rows)
    {
        var pending = new Stack<CallTreeRow>(rows);

        while (pending.Count > 0)
        {
            var row = pending.Pop();

            yield return row;

            foreach (var child in row.Children)
            {
                pending.Push(child);
            }
        }
    }

    public void Add(CallTreeRow child)
    {
        child.Parent = this;

        Children.Add(child);
    }

    public void Detach() => Parent = null;
}
