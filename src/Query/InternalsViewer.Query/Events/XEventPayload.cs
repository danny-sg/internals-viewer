namespace InternalsViewer.Query.Events;

public readonly record struct XEventPayload(string Name, string Value)
{
    public override string ToString() => Value;
}