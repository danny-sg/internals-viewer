using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnusedCodeAnalyser;

internal static class SymbolReferences
{
    public static IEnumerable<ISymbol> Get(SemanticModel model, SyntaxNode node)
    {
        var found = new List<ISymbol?>();

        switch (node)
        {
            case SimpleNameSyntax:
            case BaseObjectCreationExpressionSyntax:
            case ElementAccessExpressionSyntax:
            case ImplicitElementAccessSyntax:
            case BinaryExpressionSyntax:
            case PrefixUnaryExpressionSyntax:
            case PostfixUnaryExpressionSyntax:
            case CastExpressionSyntax:
            case ConstructorInitializerSyntax:
            case PrimaryConstructorBaseTypeSyntax:
            case AttributeSyntax:
                AddSymbolInfo(found, model.GetSymbolInfo(node));

                break;
            case AssignmentExpressionSyntax assignment:
                AddSymbolInfo(found, model.GetSymbolInfo(assignment));

                if (assignment.Left is TupleExpressionSyntax or DeclarationExpressionSyntax)
                {
                    AddDeconstruction(found, model.GetDeconstructionInfo(assignment));
                }

                break;
            case CommonForEachStatementSyntax forEach:
                var info = model.GetForEachStatementInfo(forEach);

                found.AddRange([info.GetEnumeratorMethod, info.MoveNextMethod, info.CurrentProperty, info.DisposeMethod]);

                if (forEach is ForEachVariableStatementSyntax variables)
                {
                    AddDeconstruction(found, model.GetDeconstructionInfo(variables));
                }

                break;
            case AwaitExpressionSyntax awaitExpression:
                var awaitInfo = model.GetAwaitExpressionInfo(awaitExpression);

                found.AddRange([awaitInfo.GetAwaiterMethod, awaitInfo.GetResultMethod, awaitInfo.IsCompletedProperty]);

                break;
            case InitializerExpressionSyntax initializer when initializer.IsKind(SyntaxKind.CollectionInitializerExpression):
                foreach (var expression in initializer.Expressions)
                {
                    AddSymbolInfo(found, model.GetCollectionInitializerSymbolInfo(expression));
                }

                break;
        }

        if (node is ExpressionSyntax expressionNode && model.GetConversion(expressionNode).MethodSymbol is { } conversion)
        {
            found.Add(conversion);
        }

        return found.OfType<ISymbol>().SelectMany(Normalise);
    }

    public static ISymbol? GetEnclosing(SemanticModel model, SyntaxNode node)
    {
        foreach (var ancestor in node.AncestorsAndSelf())
        {
            switch (ancestor)
            {
                case MethodDeclarationSyntax:
                case ConstructorDeclarationSyntax:
                case DestructorDeclarationSyntax:
                case OperatorDeclarationSyntax:
                case ConversionOperatorDeclarationSyntax:
                case PropertyDeclarationSyntax:
                case IndexerDeclarationSyntax:
                case EventDeclarationSyntax:
                case EnumMemberDeclarationSyntax:
                case BaseTypeDeclarationSyntax:
                case DelegateDeclarationSyntax:
                    return model.GetDeclaredSymbol(ancestor);
                case BaseFieldDeclarationSyntax field:
                    return model.GetDeclaredSymbol(field.Declaration.Variables[0]);
                case ParameterSyntax parameter when parameter.Parent?.Parent is TypeDeclarationSyntax record:
                    var recordType = model.GetDeclaredSymbol(record);

                    return recordType?.GetMembers(parameter.Identifier.ValueText).OfType<IPropertySymbol>().FirstOrDefault()
                           ?? (ISymbol?)recordType;
            }
        }

        return null;
    }

    public static IEnumerable<ISymbol> GetBases(ISymbol member)
    {
        switch (member)
        {
            case IMethodSymbol method:
                if (method.OverriddenMethod is { } overriddenMethod)
                {
                    yield return overriddenMethod.OriginalDefinition;
                }

                foreach (var implemented in method.ExplicitInterfaceImplementations)
                {
                    yield return implemented.OriginalDefinition;
                }

                break;
            case IPropertySymbol property:
                if (property.OverriddenProperty is { } overriddenProperty)
                {
                    yield return overriddenProperty.OriginalDefinition;
                }

                foreach (var implemented in property.ExplicitInterfaceImplementations)
                {
                    yield return implemented.OriginalDefinition;
                }

                break;
            case IEventSymbol eventSymbol:
                if (eventSymbol.OverriddenEvent is { } overriddenEvent)
                {
                    yield return overriddenEvent.OriginalDefinition;
                }

                break;
        }

        if (member.ContainingType is not { } type)
        {
            yield break;
        }

        foreach (var interfaceType in type.AllInterfaces)
        {
            foreach (var interfaceMember in interfaceType.GetMembers())
            {
                var implementation = type.FindImplementationForInterfaceMember(interfaceMember);

                if (SymbolEqualityComparer.Default.Equals(implementation?.OriginalDefinition, member.OriginalDefinition))
                {
                    yield return interfaceMember.OriginalDefinition;
                }
            }
        }
    }

    private static void AddSymbolInfo(List<ISymbol?> found, SymbolInfo info)
    {
        if (info.Symbol is { } symbol)
        {
            found.Add(symbol);
        }
        else
        {
            found.AddRange(info.CandidateSymbols);
        }
    }

    private static void AddDeconstruction(List<ISymbol?> found, DeconstructionInfo info)
    {
        found.Add(info.Method);

        foreach (var nested in info.Nested)
        {
            AddDeconstruction(found, nested);
        }
    }

    private static IEnumerable<ISymbol> Normalise(ISymbol symbol)
    {
        switch (symbol)
        {
            case IAliasSymbol alias:
                return Normalise(alias.Target);
            case IMethodSymbol { MethodKind: MethodKind.LocalFunction or MethodKind.AnonymousFunction }:
                return [];
            case IMethodSymbol method:
                var original = method.ReducedFrom ?? method;

                if (original.AssociatedSymbol is IPropertySymbol or IEventSymbol && original.MethodKind is not MethodKind.Ordinary)
                {
                    return [original.AssociatedSymbol.OriginalDefinition];
                }

                return original.MethodKind == MethodKind.Constructor
                    ? [original.OriginalDefinition, original.ContainingType.OriginalDefinition]
                    : [original.OriginalDefinition];
            case INamedTypeSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol:
                return [symbol.OriginalDefinition];
            default:
                return [];
        }
    }
}
