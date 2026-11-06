using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace UnusedCodeAnalyser;

internal static class Analysis
{
    private static readonly string[] IgnoredWorkspaceMessages =
    [
        "is not associated with a language",
        "Cannot resolve Assembly or Windows Metadata file"
    ];

    public static async Task<int> RunAsync(string solutionPath, string solutionDirectory, string outputDirectory, HashSet<string> scope)
    {
        var properties = new Dictionary<string, string>
        {
            ["Configuration"] = "Release",
            ["Platform"] = "x64",
            ["EnableMsixTooling"] = "false",
            ["OutDir"] = IsolatedOutput.OutDir,
            ["IntermediateOutputPath"] = IsolatedOutput.IntermediateOutputPath
        };

        using var workspace = MSBuildWorkspace.Create(properties);

        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure
                && !IgnoredWorkspaceMessages.Any(m => e.Diagnostic.Message.Contains(m, StringComparison.Ordinal)))
            {
                Console.Error.WriteLine($"Workspace: {e.Diagnostic.Message}");
            }
        });

        var solution = await workspace.OpenSolutionAsync(solutionPath);

        var projects = solution.Projects
                               .Where(p => p.Language == LanguageNames.CSharp)
                               .GroupBy(p => p.AssemblyName)
                               .Select(g => g.First())
                               .ToList();

        var missing = scope.Where(s => projects.All(p => p.AssemblyName != s)).ToList();

        if (missing.Count > 0)
        {
            Console.Error.WriteLine($"Not in the solution: {string.Join(", ", missing)}");

            return 1;
        }

        var graph = new ReferenceGraph(scope);

        foreach (var project in projects.Where(p => !IsTest(p.AssemblyName) && p.FilePath is not null))
        {
            foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(project.FilePath)!, "*.xaml", SearchOption.AllDirectories))
            {
                if (!IsBuildOutput(file))
                {
                    graph.AddXaml(File.ReadAllText(file));
                }
            }
        }

        var failedProjects = 0;

        foreach (var project in projects)
        {
            var compilation = (await project.GetCompilationAsync())!;

            var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            var generated = (await project.GetSourceGeneratedDocumentsAsync()).ToList();

            Console.WriteLine($"{project.AssemblyName}: {project.Documents.Count()} documents, "
                              + $"{generated.Count} generated, {errors.Count} errors");

            if (errors.Count > 0)
            {
                failedProjects++;

                foreach (var error in errors.Take(5))
                {
                    Console.WriteLine($"    {error}");
                }
            }

            var trees = new List<(SyntaxTree Tree, bool IsGenerated)>();

            foreach (var document in project.Documents)
            {
                trees.Add(((await document.GetSyntaxTreeAsync())!, false));
            }

            foreach (var document in generated)
            {
                var tree = (await document.GetSyntaxTreeAsync())!;

                graph.AddGeneratedTree(tree.FilePath);

                trees.Add((tree, true));
            }

            var isTest = IsTest(project.AssemblyName);

            var inScope = scope.Contains(project.AssemblyName);

            Parallel.ForEach(trees, t =>
            {
                var model = compilation.GetSemanticModel(t.Tree);

                var root = t.Tree.GetRoot();

                if (inScope)
                {
                    graph.AddDeclarations(model, root, t.IsGenerated);
                }

                graph.AddReferences(model, root, project.AssemblyName, isTest);
            });
        }

        graph.Propagate();

        graph.Write(outputDirectory, solutionDirectory);

        if (failedProjects > 0)
        {
            Console.WriteLine($"{failedProjects} projects did not compile, so references from their broken code are missing");

            return 2;
        }

        return 0;
    }

    public static bool IsTest(string assemblyName)
        => assemblyName.EndsWith(".Tests", StringComparison.Ordinal) || assemblyName.Contains(".Tests.", StringComparison.Ordinal);

    private static bool IsBuildOutput(string path)
        => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
           || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
}
