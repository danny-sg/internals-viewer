namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed record ValueDescription(string Text, string? ClassName = null)
{
    public static implicit operator ValueDescription(string text) => new(text);
}
