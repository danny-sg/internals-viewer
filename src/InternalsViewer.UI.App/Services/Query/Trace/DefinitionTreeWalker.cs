using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Definitions;

namespace InternalsViewer.UI.App.Services.Query.Trace;

public static class DefinitionTreeWalker
{
    public static IReadOnlyList<IteratorDefinition> ChildrenOf(IteratorDefinition definition)
        => definition switch
        {
            NestedLoopsDefinition loops => [loops.Outer, loops.Inner],
            MergeJoinDefinition merge => [merge.Outer.Source, merge.Inner.Source],
            HashMatchDefinition hash => [hash.Build.Source, hash.Probe.Source],
            ConcatenationDefinition concatenation => concatenation.Inputs,
            UnaryDefinition unary => [unary.Source],
            _ => []
        };

    public static (IteratorDefinition? Outer, IteratorDefinition? Inner) Inputs(IteratorDefinition definition)
        => definition switch
        {
            NestedLoopsDefinition loops => (loops.Outer, loops.Inner),
            MergeJoinDefinition merge => (merge.Outer.Source, merge.Inner.Source),
            HashMatchDefinition hash => (hash.Build.Source, hash.Probe.Source),
            ConcatenationDefinition concatenation
                => (concatenation.Inputs.Count > 0 ? concatenation.Inputs[0] : null,
                    concatenation.Inputs.Count > 1 ? concatenation.Inputs[1] : null),
            UnaryDefinition unary => (unary.Source, null),
            _ => (null, null)
        };
}
