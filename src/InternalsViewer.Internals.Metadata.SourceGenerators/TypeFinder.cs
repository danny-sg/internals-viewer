using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace InternalsViewer.Internals.Metadata.SourceGenerators;

public class TypeFinder : ISyntaxReceiver
{
    public List<RecordDeclarationSyntax> LoadTypes { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is RecordDeclarationSyntax type)
        {
            if (type.AttributeLists
                .SelectMany(s=>s.Attributes)
                .Any(a => a.GetText().ToString().EndsWith("InternalsMetadata")))
            {
                LoadTypes.Add(type);
            }
        }
    }
}