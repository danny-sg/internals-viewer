using InternalsViewer.UI.App.Models.Query.CallStack.Signatures.Enums;

namespace InternalsViewer.UI.App.Models.Query.CallStack.Signatures;

/// <summary>
/// One piece of a member signature, classified for display
/// </summary>
public sealed record SignatureToken(SignatureTokenType Type, string Text);
