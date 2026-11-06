namespace InternalsViewer.Query.FullTrace.Symbols;

public sealed record ClassMemberReference(ClassMember Member, string ClassName, bool IsOverloaded)
{
    public string QualifiedName
        => ClassName.Length > 0 ? $"{Member.Module}!{ClassName}::{Member.Name}" : $"{Member.Module}!{Member.Name}";
}
