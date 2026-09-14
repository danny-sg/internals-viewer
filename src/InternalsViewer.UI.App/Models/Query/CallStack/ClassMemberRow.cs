using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

/// <summary>
/// A member as the Members pane lists it, with the module shown only when the listing spans more than one
/// </summary>
public sealed record ClassMemberRow(string Prefix, ClassMember Member)
{
    public string Signature => Member.Signature;
}
