using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

/// <summary>
/// A member as the Members pane lists it, with the module shown only when the listing spans more than one
/// </summary>
public sealed record ClassMemberRow(string Prefix, ClassMember Member, string ClassName, bool IsOverloaded)
{
    public string Signature => Member.Signature;

    public bool IsFunction => Member.IsFunction;

    public bool IsOverloadedFunction => Member.IsFunction && IsOverloaded;
}
