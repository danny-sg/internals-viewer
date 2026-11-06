using InternalsViewer.UI.App.Models.Query.Trace.Calls.Signatures.Enums;

namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Signatures;

/// <summary>
/// One piece of a member signature, classified for display
/// </summary>
public sealed record SignatureToken(SignatureTokenType Type, string Text);
