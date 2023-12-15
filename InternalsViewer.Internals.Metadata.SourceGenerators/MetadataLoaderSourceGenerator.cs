using Microsoft.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.Internals.Metadata.SourceGenerators;

/// <summary>
/// Source Generator to automatically generate Loader classes for records marked with the GenerateRecordLoader attribute
/// </summary>
[Generator]
public class InternalRecordLoadGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        var types = ((TypeFinder)context.SyntaxReceiver)?.LoadTypes ?? new();

        foreach (var type in types)
        {
            var model = context.Compilation.GetSemanticModel(type.SyntaxTree);

            var symbol = model.GetDeclaredSymbol(type);

            var ns = GetNamespace(symbol?.ContainingNamespace);

            context.AddSource($"{type.Identifier.ValueText}Loader.g.cs", GenerateLoader(type, ns));
        }
    }

    private string GenerateLoader(RecordDeclarationSyntax source, string ns)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine("using InternalsViewer.Internals.Engine.Records.Data;");
        
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"namespace {ns};");
        stringBuilder.AppendLine();

        stringBuilder.AppendLine($"public static class {source.Identifier.ValueText}Loader");
        stringBuilder.AppendLine("{");
        stringBuilder.AppendLine($"    public static {source.Identifier.ValueText} Load(DataRecord record)");
        stringBuilder.AppendLine("    {");
        stringBuilder.AppendLine($"        var result = new {source.Identifier.ValueText}();");
        stringBuilder.AppendLine();

        foreach (var property in source.Members.OfType<PropertyDeclarationSyntax>())
        {
            var propertyName = property.Identifier.ValueText;

            stringBuilder.AppendLine($"        result.{propertyName} = record.GetValue<{property.Type}>(\"{property.Identifier.ValueText}\");");
        }

        stringBuilder.AppendLine();
        stringBuilder.AppendLine("        return result;");
        stringBuilder.AppendLine("    }");
        stringBuilder.AppendLine("}");

        return stringBuilder.ToString();
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new TypeFinder());
    }

    private static string GetNamespace(ISymbol symbol) =>
        symbol.ContainingNamespace == null 
            ? symbol.Name 
            : (GetNamespace(symbol.ContainingNamespace) + "." + symbol.Name).Trim('.');
}

public class TypeFinder : ISyntaxReceiver
{
    public List<RecordDeclarationSyntax> LoadTypes { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is RecordDeclarationSyntax type)
        {
            if (type.AttributeLists
                    .SelectMany(s=>s.Attributes)
                    .Any(a => a.GetText().ToString().EndsWith("GenerateRecordLoader")))
            {
                LoadTypes.Add(type);
            }
        }
    }
}