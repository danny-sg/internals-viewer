using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnusedCodeAnalyser;

internal sealed partial class ReferenceGraph(HashSet<string> scope)
{
    private const string ExternalPrefix = "EXT|";

    private ConcurrentDictionary<string, Declaration> Declarations { get; } = new();

    private ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> Edges { get; } = new();

    private ConcurrentDictionary<string, byte> Roots { get; } = new();

    private ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> TestReferences { get; } = new();

    private ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> ScopeReferences { get; } = new();

    private ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> Incoming { get; } = new();

    private ConcurrentDictionary<string, byte> GeneratedTrees { get; } = new();

    private HashSet<string> XamlMemberNames { get; } = [];

    private HashSet<string> XamlTypeNames { get; } = [];

    private HashSet<string> Live { get; } = [];

    public void AddGeneratedTree(string path) => GeneratedTrees.TryAdd(path, 0);

    public void AddXaml(string text)
    {
        foreach (Match binding in XamlBinding().Matches(text))
        {
            foreach (Match identifier in Identifier().Matches(binding.Groups[1].Value))
            {
                XamlMemberNames.Add(identifier.Value);
            }
        }

        foreach (Match type in XamlPrefixedName().Matches(text))
        {
            XamlTypeNames.Add(type.Groups[1].Value);
        }
    }

    public void AddDeclarations(SemanticModel model, SyntaxNode root, bool isGenerated)
    {
        foreach (var node in root.DescendantNodes())
        {
            if (node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax && model.GetDeclaredSymbol(node) is INamedTypeSymbol type)
            {
                AddType(type, isGenerated);
            }
        }
    }

    public void AddReferences(SemanticModel model, SyntaxNode root, string assemblyName, bool isTest)
    {
        var inScope = scope.Contains(assemblyName);

        var fileName = Path.GetFileName(root.SyntaxTree.FilePath);

        foreach (var node in root.DescendantNodes())
        {
            if (node.Ancestors().Any(a => a is UsingDirectiveSyntax))
            {
                continue;
            }

            foreach (var symbol in SymbolReferences.Get(model, node))
            {
                if (!IsScopeSymbol(symbol) || GetId(symbol) is not { } target)
                {
                    continue;
                }

                if (isTest)
                {
                    TestReferences.GetOrAdd(target, _ => new()).TryAdd($"{assemblyName}:{fileName}", 0);

                    continue;
                }

                var from = SymbolReferences.GetEnclosing(model, node);

                var fromId = from is not null && inScope && IsScopeSymbol(from) ? GetId(from) : null;

                if (fromId is null)
                {
                    Incoming.GetOrAdd(target, _ => new())
                            .TryAdd(ExternalPrefix + assemblyName + "|" + (from is null ? fileName : GetId(from) ?? fileName), 0);

                    Roots.TryAdd(target, 0);

                    continue;
                }

                Incoming.GetOrAdd(target, _ => new()).TryAdd(fromId, 0);

                if (fromId == target)
                {
                    continue;
                }

                Edges.GetOrAdd(fromId, _ => new()).TryAdd(target, 0);

                ScopeReferences.GetOrAdd(target, _ => new()).TryAdd(fromId, 0);
            }
        }
    }

    public void Propagate()
    {
        var queue = new Queue<string>();

        void Mark(string id)
        {
            if (Live.Add(id))
            {
                queue.Enqueue(id);
            }
        }

        foreach (var root in Roots.Keys)
        {
            Mark(root);
        }

        foreach (var type in Declarations.Values.Where(d => d.IsType && XamlTypeNames.Contains(d.Name)))
        {
            Mark(type.Id);
        }

        var members = Declarations.Values.Where(d => !d.IsType).ToList();

        var changed = true;

        while (changed)
        {
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();

                if (Edges.TryGetValue(id, out var targets))
                {
                    foreach (var target in targets.Keys)
                    {
                        Mark(target);
                    }
                }

                if (Declarations.TryGetValue(id, out var declaration) && declaration.ContainingType is not null)
                {
                    Mark(declaration.ContainingType);
                }
            }

            changed = false;

            foreach (var member in members)
            {
                if (Live.Contains(member.Id) || member.ContainingType is null || !Live.Contains(member.ContainingType))
                {
                    continue;
                }

                if (member.IsConstructor
                    || member.HasExternalBase
                    || member.IsGenerated
                    || member.Bases.Any(Live.Contains)
                    || XamlMemberNames.Contains(member.Name))
                {
                    Mark(member.Id);

                    changed = true;
                }
            }
        }
    }

    private static string? GetId(ISymbol symbol)
        => symbol.OriginalDefinition.GetDocumentationCommentId() is { } id ? $"{symbol.ContainingAssembly?.Name}|{id}" : null;

    private void AddType(INamedTypeSymbol type, bool isGenerated)
    {
        if (GetId(type) is not { } id || GetSourceLocation(type) is not { } location)
        {
            return;
        }

        var chain = new List<string>();

        for (var baseType = type.BaseType; baseType is { SpecialType: not SpecialType.System_Object }; baseType = baseType.BaseType)
        {
            chain.Add(baseType.Name);
        }

        Declarations.TryAdd(id, new Declaration
        {
            Id = id,
            Name = type.Name,
            Display = type.ToDisplayString(),
            Kind = type.TypeKind + (type.IsRecord ? " record" : string.Empty),
            FilePath = location.FilePath,
            Line = location.Line,
            IsGenerated = isGenerated && type.Locations.All(l => !l.IsInSource || IsGeneratedPath(l.SourceTree?.FilePath)),
            ContainingType = type.ContainingType is null ? null : GetId(type.ContainingType),
            IsType = true,
            IsAbstract = type.IsAbstract,
            IsStatic = type.IsStatic,
            BaseTypeId = type.BaseType is { } baseOfType && IsScopeSymbol(baseOfType) ? GetId(baseOfType) : null,
            BaseTypeChain = string.Join(" : ", chain)
        });

        var isLayout = type.GetAttributes().Any(a => a.AttributeClass?.Name == "StructLayoutAttribute")
                       || type.ContainingNamespace.ToDisplayString().EndsWith(".Native", StringComparison.Ordinal);

        foreach (var member in type.GetMembers())
        {
            if (member is INamedTypeSymbol
                || member is IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd
                                             or MethodKind.EventRemove or MethodKind.EventRaise }
                || GetId(member) is not { } memberId)
            {
                continue;
            }

            var memberLocation = GetSourceLocation(member) ?? location;

            var declaration = new Declaration
            {
                Id = memberId,
                Name = member.Name,
                Display = member.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                Kind = member.Kind.ToString(),
                FilePath = memberLocation.FilePath,
                Line = memberLocation.Line,
                IsGenerated = type.TypeKind == TypeKind.Enum
                              || member.IsImplicitlyDeclared
                              || member.DeclaringSyntaxReferences.Length == 0
                              || member.Locations.All(l => !l.IsInSource || IsGeneratedPath(l.SourceTree?.FilePath)),
                ContainingType = id,
                IsConstructor = member is IMethodSymbol { MethodKind: MethodKind.Constructor
                                                          or MethodKind.StaticConstructor
                                                          or MethodKind.Destructor },
                IsPositional = member is IPropertySymbol && member.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is ParameterSyntax),
                IsLayout = isLayout
            };

            foreach (var baseMember in SymbolReferences.GetBases(member))
            {
                if (IsScopeSymbol(baseMember) && GetId(baseMember) is { } baseId)
                {
                    declaration.Bases.Add(baseId);
                }
                else
                {
                    declaration.HasExternalBase = true;
                }
            }

            Declarations.TryAdd(memberId, declaration);
        }
    }

    private (string FilePath, int Line)? GetSourceLocation(ISymbol symbol)
        => symbol.Locations
                 .Where(l => l.IsInSource && !IsGeneratedPath(l.SourceTree?.FilePath))
                 .Select(l => (FilePath: l.SourceTree!.FilePath, Line: l.GetLineSpan().StartLinePosition.Line + 1))
                 .OrderBy(l => l.FilePath.Length)
                 .Cast<(string FilePath, int Line)?>()
                 .FirstOrDefault();

    private bool IsScopeSymbol(ISymbol symbol) => symbol.ContainingAssembly is { } assembly && scope.Contains(assembly.Name);

    private bool IsGeneratedPath(string? path)
        => path is null
           || GeneratedTrees.ContainsKey(path)
           || path.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase)
           || path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\{(?:x:Bind|Binding)\b([^}]*)\}")]
    private static partial Regex XamlBinding();

    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex Identifier();

    [GeneratedRegex(@"\b[A-Za-z_][A-Za-z0-9_]*:([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex XamlPrefixedName();
}
