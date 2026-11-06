using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MemberRemover;

internal sealed class LeadingBlankLineRemover : CSharpSyntaxRewriter
{
    public static SyntaxAnnotation FirstMemberRemoved { get; } = new(nameof(FirstMemberRemoved));

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        => Fix((TypeDeclarationSyntax)base.VisitClassDeclaration(node)!);

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
        => Fix((TypeDeclarationSyntax)base.VisitStructDeclaration(node)!);

    public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
        => Fix((TypeDeclarationSyntax)base.VisitRecordDeclaration(node)!);

    public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        => Fix((TypeDeclarationSyntax)base.VisitInterfaceDeclaration(node)!);

    private static SyntaxNode Fix(TypeDeclarationSyntax type)
    {
        if (type.Members.Count == 0 || !type.HasAnnotation(FirstMemberRemoved))
        {
            return type;
        }

        var first = type.Members[0];

        var trivia = first.GetLeadingTrivia();

        var index = 0;

        while (index < trivia.Count && trivia[index].IsKind(SyntaxKind.WhitespaceTrivia))
        {
            index++;
        }

        if (index >= trivia.Count || !trivia[index].IsKind(SyntaxKind.EndOfLineTrivia))
        {
            return type;
        }

        return type.ReplaceNode(first, first.WithLeadingTrivia(SyntaxFactory.TriviaList(trivia.Skip(index + 1))));
    }
}
