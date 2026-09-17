using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace InternalsViewer.Query.SourceGenerators;

/// <summary>
/// Generates a CollectProperties method on every partial event record that marks properties with EventPropertyAttribute
/// </summary>
[Generator]
public sealed class EventPropertyGenerator : IIncrementalGenerator
{
    private const string AttributeName = "InternalsViewer.Query.Events.Properties.EventPropertyAttribute";

    private static readonly DiagnosticDescriptor NotPartial = new("IVQ0001",
                                                                  "Event record must be partial",
                                                                  "'{0}' marks properties with [EventProperty] but is not declared partial",
                                                                  "InternalsViewer",
                                                                  DiagnosticSeverity.Warning,
                                                                  isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var properties = context.SyntaxProvider
                                .ForAttributeWithMetadataName(AttributeName,
                                                              static (node, _) => node is PropertyDeclarationSyntax,
                                                              static (syntaxContext, _) => Describe(syntaxContext))
                                .Where(static p => p is not null)
                                .Select(static (p, _) => p!)
                                .Collect();

        context.RegisterSourceOutput(properties, static (productionContext, all) => Emit(productionContext, all));
    }

    private static PropertyModel? Describe(GeneratorAttributeSyntaxContext syntaxContext)
    {
        if (syntaxContext.TargetSymbol is not IPropertySymbol property)
        {
            return null;
        }

        var attribute = syntaxContext.Attributes.FirstOrDefault();

        if (attribute is null || attribute.ConstructorArguments.Length == 0)
        {
            return null;
        }

        var displayName = attribute.ConstructorArguments[0].Value as string ?? property.Name;

        var type = "Default";

        string? format = null;

        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key == "Type" && named.Value.Type is INamedTypeSymbol enumType && named.Value.Value is int ordinal)
            {
                type = enumType.GetMembers()
                               .OfType<IFieldSymbol>()
                               .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, ordinal))?.Name ?? type;
            }
            else if (named.Key == "Format")
            {
                format = named.Value.Value as string;
            }
        }

        var link = LinkKind(property.Type);

        if (link is not null)
        {
            type = link;
        }

        var containing = property.ContainingType;

        var isPartial = containing.DeclaringSyntaxReferences
                                  .Select(r => r.GetSyntax())
                                  .OfType<TypeDeclarationSyntax>()
                                  .Any(t => t.Modifiers.Any(SyntaxKind.PartialKeyword));

        return new PropertyModel(containing.ToDisplayString(),
                                 containing.Name,
                                 containing.ContainingNamespace.IsGlobalNamespace ? string.Empty : containing.ContainingNamespace.ToDisplayString(),
                                 containing.IsRecord,
                                 containing.BaseType is null || containing.BaseType.SpecialType == SpecialType.System_Object,
                                 isPartial,
                                 containing.Locations.FirstOrDefault(),
                                 property.Name,
                                 displayName,
                                 type,
                                 format,
                                 Expression(property),
                                 link,
                                 property.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0);
    }

    private static string? LinkKind(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named)
        {
            type = named.TypeArguments[0];
        }

        return type.Name is "PageAddress" or "RowIdentifier" ? type.Name : null;
    }

    private static string Expression(IPropertySymbol property)
    {
        var type = property.Type;

        var nullable = false;

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named)
        {
            type = named.TypeArguments[0];

            nullable = true;
        }

        var suffix = nullable ? "?" : string.Empty;

        if (type.TypeKind == TypeKind.Enum)
        {
            return $"EventPropertyFormatter.Enum({property.Name})";
        }

        return type.SpecialType switch
        {
            SpecialType.System_Boolean => $"EventPropertyFormatter.Bool({property.Name})",
            SpecialType.System_DateTime when !nullable => $"EventPropertyFormatter.Time({property.Name})",
            SpecialType.System_String => $"EventPropertyFormatter.Text({property.Name})",
            SpecialType.System_Int64 or SpecialType.System_Int32 or SpecialType.System_Int16 or SpecialType.System_Byte
                => $"EventPropertyFormatter.Number((long{suffix}){property.Name}, TYPE, FORMAT)",
            SpecialType.System_UInt64 or SpecialType.System_UInt32 or SpecialType.System_UInt16
                => $"EventPropertyFormatter.Number((ulong{suffix}){property.Name}, TYPE, FORMAT)",
            SpecialType.System_Double or SpecialType.System_Single or SpecialType.System_Decimal
                => $"EventPropertyFormatter.Number((double{suffix}){property.Name}, TYPE, FORMAT)",
            _ => $"EventPropertyFormatter.Text({property.Name})"
        };
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<PropertyModel> all)
    {
        foreach (var group in all.GroupBy(p => p.TypeFullName))
        {
            var first = group.First();

            if (!first.IsPartial)
            {
                context.ReportDiagnostic(Diagnostic.Create(NotPartial, first.TypeLocation, first.TypeName));

                continue;
            }

            var builder = new StringBuilder();

            builder.AppendLine("// <auto-generated />");
            builder.AppendLine("#nullable enable");
            builder.AppendLine();
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using InternalsViewer.Query.Events.Properties;");
            builder.AppendLine();

            if (first.Namespace.Length > 0)
            {
                builder.AppendLine($"namespace {first.Namespace};");
                builder.AppendLine();
            }

            builder.AppendLine($"partial {(first.IsRecord ? "record" : "class")} {first.TypeName}");
            builder.AppendLine("{");
            builder.AppendLine($"    public {(first.IsRoot ? "virtual" : "override")} void CollectProperties(List<EventProperty> properties)");
            builder.AppendLine("    {");

            if (!first.IsRoot)
            {
                builder.AppendLine("        base.CollectProperties(properties);");
                builder.AppendLine();
            }

            foreach (var property in group.OrderBy(p => p.Order))
            {
                var format = property.Format is null ? "null" : $"\"{property.Format.Replace("\"", "\\\"")}\"";

                var expression = property.Expression
                                         .Replace("TYPE", $"EventPropertyType.{property.Type}")
                                         .Replace("FORMAT", format);

                var link = property.Link switch
                {
                    "PageAddress" => $", Page: {property.PropertyName}",
                    "RowIdentifier" => $", Row: {property.PropertyName}",
                    _ => string.Empty
                };

                builder.AppendLine($"        properties.Add(new EventProperty(\"{property.DisplayName.Replace("\"", "\\\"")}\", {expression}, EventPropertyType.{property.Type}{link}));");
            }

            builder.AppendLine("    }");
            builder.AppendLine("}");

            context.AddSource($"{first.TypeName}.EventProperties.g.cs", builder.ToString());
        }
    }

    private sealed class PropertyModel(string typeFullName,
                                       string typeName,
                                       string ns,
                                       bool isRecord,
                                       bool isRoot,
                                       bool isPartial,
                                       Location? typeLocation,
                                       string propertyName,
                                       string displayName,
                                       string type,
                                       string? format,
                                       string expression,
                                       string? link,
                                       int order)
    {
        public string TypeFullName { get; } = typeFullName;

        public string TypeName { get; } = typeName;

        public string Namespace { get; } = ns;

        public bool IsRecord { get; } = isRecord;

        public bool IsRoot { get; } = isRoot;

        public bool IsPartial { get; } = isPartial;

        public Location? TypeLocation { get; } = typeLocation;

        public string PropertyName { get; } = propertyName;

        public string DisplayName { get; } = displayName;

        public string Type { get; } = type;

        public string? Format { get; } = format;

        public string Expression { get; } = expression;

        public string? Link { get; } = link;

        public int Order { get; } = order;
    }
}
