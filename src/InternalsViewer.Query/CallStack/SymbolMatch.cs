namespace InternalsViewer.Query.CallStack;

/// <summary>
/// A public symbol found by name, described the way the Members pane describes a member
/// </summary>
/// <remarks>
/// <see cref="ClassName"/> is empty for a symbol outside any class, such as a free function or a global.
/// </remarks>
public sealed record SymbolMatch(string ClassName, ClassMember Member);
