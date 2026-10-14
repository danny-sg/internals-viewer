using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class CallTreeRow(object content)
{
    public object Content { get; } = content;

    public List<CallTreeRow> Children { get; } = [];

    public CallTreeRow? Parent { get; private set; }

    public bool IsRevealed { get; init; }

    public void Add(CallTreeRow child)
    {
        child.Parent = this;

        Children.Add(child);
    }

    public void Detach() => Parent = null;
}
