namespace InternalsViewer.UI.App.Models;

public sealed record StringValue(string Text, bool IsTruncated)
{
    public override string ToString() => IsTruncated ? $"{Text} (Truncated)" : Text;
}
