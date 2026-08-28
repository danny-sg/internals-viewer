using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.UI.App.Services.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

/// <summary>
/// Finds the operators that create a batch rather than passing one through
/// </summary>
public static class BatchOwners
{
    public static IReadOnlyList<IteratorDefinition> Find(IteratorDefinition definition)
    {
        var found = new List<IteratorDefinition>();

        Collect(definition, found);

        return found;
    }

    public static bool OwnsBatch(IteratorDefinition definition)
        => definition is ColumnstoreScanDefinition or BatchHashAggregateDefinition;

    private static void Collect(IteratorDefinition definition, List<IteratorDefinition> found)
    {
        foreach (var child in DefinitionTreeWalker.ChildrenOf(definition))
        {
            Collect(child, found);
        }

        if (OwnsBatch(definition))
        {
            found.Add(definition);
        }
    }
}
