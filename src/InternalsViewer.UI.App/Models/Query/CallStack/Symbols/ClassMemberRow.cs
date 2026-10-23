using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Models.Query.CallStack.Symbols;

/// <summary>
/// A member as the Members pane lists it, with the module shown only when the listing spans more than one
/// </summary>
public sealed record ClassMemberRow(string Prefix, ClassMember Member, string ClassName, bool IsOverloaded)
{
    public string ClassPrefix { get; set; } = string.Empty;

    public string Highlight { get; set; } = string.Empty;

    public string Signature => Member.Signature;

    public ClassMemberReference Reference => new(Member, ClassName, IsOverloaded);
}
