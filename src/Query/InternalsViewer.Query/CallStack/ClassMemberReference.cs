namespace InternalsViewer.Query.CallStack;

public sealed record ClassMemberReference(ClassMember Member, string ClassName, bool IsOverloaded);
