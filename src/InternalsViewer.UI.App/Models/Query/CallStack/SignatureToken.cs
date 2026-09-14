namespace InternalsViewer.UI.App.Models.Query.CallStack;

public enum SignatureTokenType
{
    Name,
    Keyword,
    Type,
    Punctuation
}

/// <summary>
/// One piece of a member signature, classified for display
/// </summary>
public sealed record SignatureToken(SignatureTokenType Type, string Text);
