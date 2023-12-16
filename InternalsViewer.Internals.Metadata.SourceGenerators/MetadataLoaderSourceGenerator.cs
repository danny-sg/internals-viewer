﻿using Microsoft.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System.Text;
using System.Data;

namespace InternalsViewer.Internals.Metadata.SourceGenerators;

/// <summary>
/// Source Generator to automatically generate Loader classes for records marked with the GenerateRecordLoader attribute
/// </summary>
[Generator]
public class InternalRecordLoadGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new TypeFinder());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var types = ((TypeFinder)context.SyntaxReceiver)?.LoadTypes ?? new();

        foreach (var type in types)
        {
            var model = context.Compilation.GetSemanticModel(type.SyntaxTree);

            var symbol = model.GetDeclaredSymbol(type);

            var ns = GetNamespace(symbol?.ContainingNamespace);

            var name = $"{type.Identifier.ValueText}.g.cs";

            context.AddSource(name, GenerateLoader(context, type, ns));
        }
    }

    private static string GenerateLoader(GeneratorExecutionContext context, TypeDeclarationSyntax source, string ns)
    {
        var loaderBuilder = new StringBuilder();

        var structureBuilder = new StringBuilder();

        structureBuilder.AppendLine($"public static class {source.Identifier.ValueText}Structure");
        structureBuilder.AppendLine("{");
        structureBuilder.AppendLine("    public static TableStructure GetStructure(long allocationUnitId)");
        structureBuilder.AppendLine("    {");
        structureBuilder.AppendLine("        var structure = new TableStructure(allocationUnitId);");
        structureBuilder.AppendLine();
        structureBuilder.AppendLine("        var columns = new List<ColumnStructure>");
        structureBuilder.AppendLine("        {");

        loaderBuilder.AppendLine("using System;");
        loaderBuilder.AppendLine("using System.Data;");
        loaderBuilder.AppendLine("using System.Collections.Generic;");
        loaderBuilder.AppendLine("using InternalsViewer.Internals.Engine.Records.Data;");

        loaderBuilder.AppendLine();
        loaderBuilder.AppendLine($"namespace {ns};");
        loaderBuilder.AppendLine();

        loaderBuilder.AppendLine($"public static class {source.Identifier.ValueText}Loader");
        loaderBuilder.AppendLine("{");
        loaderBuilder.AppendLine($"    public static {source.Identifier.ValueText} Load(DataRecord record)");
        loaderBuilder.AppendLine("    {");
        loaderBuilder.AppendLine($"        var result = new {source.Identifier.ValueText}();");
        loaderBuilder.AppendLine();

        foreach (var property in source.Members.OfType<PropertyDeclarationSyntax>())
        {
            var propertyName = property.Identifier.ValueText;

            var attribute = property.AttributeLists
                                    .SelectMany(a => a.Attributes)
                                    .FirstOrDefault(a => a.Name.ToString() == "InternalsMetadataColumn");

            if (attribute == null)
            {
                continue;
            }

            var arguments = attribute.ArgumentList?.Arguments;

            var name = GetValue<string>(context, arguments?[0].Expression);
            var columnId = GetValue<int>(context, arguments?[1].Expression);
            var dataType = GetValue<SqlDbType>(context, arguments?[2].Expression);
            var dataLength = GetValue<int>(context, arguments?[3].Expression);
            var leafOffset = GetValue<int>(context, arguments?[4].Expression);
            var nullBit = GetValue<int>(context, arguments?[5].Expression);

            structureBuilder.AppendLine($"            new()");
            structureBuilder.AppendLine("            {");
            structureBuilder.AppendLine($"                ColumnName = \"{name}\",");
            structureBuilder.AppendLine($"                ColumnId = {columnId},");
            structureBuilder.AppendLine($"                DataType = SqlDbType.{dataType},");
            structureBuilder.AppendLine($"                DataLength = {dataLength},");
            structureBuilder.AppendLine($"                LeafOffset = {leafOffset},");
            structureBuilder.AppendLine($"                NullBit = {nullBit}");
            structureBuilder.AppendLine("            },");

            loaderBuilder.Append($"        result.{propertyName} = ");
            loaderBuilder.AppendLine($"record.GetValue<{property.Type}>(\"{name}\");");
        }

        structureBuilder.AppendLine("        };");
        structureBuilder.AppendLine();
        structureBuilder.AppendLine("        structure.Columns.AddRange(columns);");
        structureBuilder.AppendLine();
        structureBuilder.AppendLine("        return structure;");
        structureBuilder.AppendLine("    }");
        structureBuilder.AppendLine("}");

        loaderBuilder.AppendLine();
        loaderBuilder.AppendLine("        return result;");
        loaderBuilder.AppendLine("    }");
        loaderBuilder.AppendLine("}");
        loaderBuilder.AppendLine();
        loaderBuilder.AppendLine(structureBuilder.ToString());

        return loaderBuilder.ToString();
    }

    private static T GetValue<T>(GeneratorExecutionContext context, SyntaxNode expression)
    {
        if (expression == null)
        {
            return default!;
        }

        return (T)context.Compilation.GetSemanticModel(expression.SyntaxTree).GetConstantValue(expression).Value;
    }

    private static string GetNamespace(ISymbol symbol) =>
        symbol.ContainingNamespace == null
            ? symbol.Name
            : (GetNamespace(symbol.ContainingNamespace) + "." + symbol.Name).Trim('.');
}