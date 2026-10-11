namespace InternalsViewer.Query.CallStack;

/// <summary>
/// The members the symbols of one or more modules declare on a class, grouped by module
/// </summary>
public sealed record ClassMemberListing(string ClassName, IReadOnlyList<ClassMemberGroup> Groups)
{
    public int Count => Groups.Sum(g => g.Members.Count);
}

/// <summary>
/// The members one module's symbols declare on a class
/// </summary>
public sealed record ClassMemberGroup(string Module, IReadOnlyList<ClassMember> Members);

/// <summary>
/// Member of a class as one module's public symbols describe it
/// </summary>
public sealed record ClassMember(string Module, string Name, string Signature, uint Rva, bool IsFunction);
