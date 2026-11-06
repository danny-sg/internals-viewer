using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MemberRemover;

internal static class SourceFile
{
    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

    public static RemovalResult Remove(string path, IReadOnlyList<Removal> removals)
    {
        var result = new RemovalResult();

        if (!File.Exists(path))
        {
            result.Problems.Add("file not found");

            return result;
        }

        var bytes = File.ReadAllBytes(path);

        var hasByteOrderMark = bytes.AsSpan().StartsWith(ByteOrderMark);

        var text = Encoding.UTF8.GetString(bytes.AsSpan(hasByteOrderMark ? ByteOrderMark.Length : 0));

        var root = CSharpSyntaxTree.ParseText(text).GetRoot();

        var wanted = removals.ToDictionary(r => r.Line, r => r.Name);

        var members = FindMembers(root, wanted);

        foreach (var line in wanted.Keys.Where(l => members.All(m => GetLine(m) != l)))
        {
            result.Problems.Add($"no member declared on line {line} ({wanted[line]})");
        }

        foreach (var member in members)
        {
            var line = GetLine(member)!.Value;

            if (wanted[line] is { Length: > 0 } expected && expected != GetName(member))
            {
                result.Problems.Add($"line {line} declares {GetName(member)}, not {expected}");
            }

            if (member.GetLeadingTrivia().Any(t => t.IsDirective))
            {
                result.Problems.Add($"line {line} has a preprocessor directive before it, remove {GetName(member)} by hand");
            }
        }

        if (result.Problems.Count > 0)
        {
            return result;
        }

        var emptied = members.Select(m => m.Parent)
                             .OfType<TypeDeclarationSyntax>()
                             .Where(t => members.Contains(t.Members[0]))
                             .Distinct()
                             .ToList();

        var annotated = root.ReplaceNodes(emptied, (_, n) => n.WithAdditionalAnnotations(LeadingBlankLineRemover.FirstMemberRemoved));

        var updated = annotated.RemoveNodes(FindMembers(annotated, wanted), SyntaxRemoveOptions.KeepNoTrivia)!;

        updated = new LeadingBlankLineRemover().Visit(updated);

        var encoded = Encoding.UTF8.GetBytes(updated.ToFullString());

        File.WriteAllBytes(path, hasByteOrderMark ? [.. ByteOrderMark, .. encoded] : encoded);

        result.Removed.AddRange(members.Select(GetName));

        return result;
    }

    private static List<MemberDeclarationSyntax> FindMembers(SyntaxNode root, Dictionary<int, string> wanted)
        => [.. root.DescendantNodes()
                   .OfType<MemberDeclarationSyntax>()
                   .Where(m => m is not BaseNamespaceDeclarationSyntax && GetLine(m) is { } line && wanted.ContainsKey(line))];

    private static int? GetLine(MemberDeclarationSyntax member)
    {
        SyntaxToken? token = member switch
        {
            MethodDeclarationSyntax method => method.Identifier,
            PropertyDeclarationSyntax property => property.Identifier,
            FieldDeclarationSyntax field => field.Declaration.Variables[0].Identifier,
            EventFieldDeclarationSyntax eventField => eventField.Declaration.Variables[0].Identifier,
            EventDeclarationSyntax eventDeclaration => eventDeclaration.Identifier,
            ConstructorDeclarationSyntax constructor => constructor.Identifier,
            IndexerDeclarationSyntax indexer => indexer.ThisKeyword,
            BaseTypeDeclarationSyntax type => type.Identifier,
            DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.Identifier,
            _ => null
        };

        return token is { } found ? found.GetLocation().GetLineSpan().StartLinePosition.Line + 1 : null;
    }

    private static string GetName(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        FieldDeclarationSyntax field => field.Declaration.Variables[0].Identifier.ValueText,
        EventFieldDeclarationSyntax eventField => eventField.Declaration.Variables[0].Identifier.ValueText,
        EventDeclarationSyntax eventDeclaration => eventDeclaration.Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        IndexerDeclarationSyntax => "this",
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.Identifier.ValueText,
        _ => member.Kind().ToString()
    };
}
