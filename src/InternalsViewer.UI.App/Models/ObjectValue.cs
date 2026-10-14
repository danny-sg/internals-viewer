namespace InternalsViewer.UI.App.Models;

public sealed record ObjectValue(string ClassName, string Text)
{
    public override string ToString() => $"{ClassName} {Text}";
}
