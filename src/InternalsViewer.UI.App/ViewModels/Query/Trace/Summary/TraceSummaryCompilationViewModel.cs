using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace.Compilation;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.UI.App.Models.Query.Trace.Summary;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Summary;

public sealed class TraceSummaryCompilationViewModel : TraceSummaryPaneViewModel
{
    private const string CompilationTitle = "Compilation";

    private const string NotAvailable = "No Compilation";

    private const string InputName = "Input SQL";

    private const string TokensName = "Tokens";

    private const string TokenNoun = "Token";

    private const string ObjectNoun = "Object";

    private const string OperatorNoun = "Operator";

    private const string ObjectsName = "Objects";

    private const string UnknownOperator = "Unknown Operator";

    private const string KeyPrefix = "compilation";

    private const int GroupThreshold = 30;

    public TraceSummaryCompilationViewModel(MemoryHistoryViewModel history, Func<ITraceNavigator> getNavigator)
        : base(CompilationTitle, history, getNavigator)
    {
        Loader = new PlayheadLoader<List<TraceSummaryNode>>(TimeSpan.Zero, Apply, b => IsLoading = b);
    }

    private PlayheadLoader<List<TraceSummaryNode>> Loader { get; }

    protected override void Build()
    {
        if (!History.IsAvailable)
        {
            EmptyText = NotAvailable;

            return;
        }

        EmptyText = string.Empty;

        var history = History;

        Loader.Request(() => Read(history));
    }

    protected override void Clear() => Loader.Cancel();

    private void Apply(List<TraceSummaryNode> roots)
    {
        Roots = roots;

        EmptyText = roots.Count == 0 ? NotAvailable : string.Empty;
    }

    private static List<TraceSummaryNode> Read(MemoryHistoryViewModel history)
    {
        var compilation = history.GetCompilation();

        var roots = new List<TraceSummaryNode>();

        if (compilation.Text is { } text)
        {
            roots.Add(new TraceSummaryNode(InputName, $"{GetAddressText(text.Address)} · {text.Size} Bytes", $"{KeyPrefix}/input")
            {
                Block = text.Block,
                Address = text.Address,
                Position = text.Position
            });
        }

        if (compilation.Tokens.Count > 0)
        {
            var tokens = compilation.Tokens.Select((t, i) => CreateToken(t, i)).ToList();

            roots.Add(new TraceSummaryNode(TokensName, GetCountText(tokens.Count, TokenNoun), $"{KeyPrefix}/tokens")
            {
                HasChildren = true,
                Children = tokens
            });
        }

        roots.AddRange(compilation.Steps.Select(s => CreateStep(history, s, KeyPrefix)));

        return roots;
    }

    private static TraceSummaryNode CreateToken(TraceToken token, int index)
        => new(token.Text.Length > 0 ? token.Text : token.KindName,
               $"{token.KindName} · Token {token.Id} · Offset {token.Offset}",
               $"{KeyPrefix}/tokens/{index}")
        {
            Call = token.Node,
            CallIndex = token.CallIndex,
            Position = token.Position
        };

    private static TraceSummaryNode CreateStep(MemoryHistoryViewModel history, TraceCompileStep step, string parentKey)
    {
        var key = $"{parentKey}/{step.Node.Symbol}@{step.Span.From.Position}";

        var children = step.Steps.Select(s => (Position: s.Span.From.Position, Node: CreateStep(history, s, key)))
                           .Concat(step.Trees.Select(t => (t.Position, Node: CreateTree(history, t, key))))
                           .OrderBy(c => c.Position)
                           .Select(c => c.Node)
                           .ToList();

        if (step.Objects.Count > 0)
        {
            children.Add(CreateObjects(history, step, key));
        }

        return new TraceSummaryNode(step.Name, step.Detail ?? string.Empty, key)
        {
            Call = step.Node,
            CallIndex = step.Span.CallIndex,
            Position = step.Span.From.Position,
            HasChildren = children.Count > 0,
            Children = children
        };
    }

    private static TraceSummaryNode CreateObjects(MemoryHistoryViewModel history, TraceCompileStep step, string parentKey)
    {
        var key = $"{parentKey}/objects";

        List<TraceSummaryNode> children = step.Objects.Count > GroupThreshold
            ? [.. step.Objects.GroupBy(o => o.ClassName).Select(g => CreateGroup(history, step, g.ToList(), key))]
            : [.. step.Objects.Select(o => CreateObject(history, step, o, key))];

        return new TraceSummaryNode(ObjectsName, GetCountText(step.Objects.Count, ObjectNoun), key)
        {
            HasChildren = true,
            Children = children
        };
    }

    private static TraceSummaryNode CreateTree(MemoryHistoryViewModel history, TraceExpressionTree tree, string parentKey)
    {
        var key = $"{parentKey}/tree/{tree.Name}";

        return new TraceSummaryNode(tree.Name, GetCountText(Count(tree.Root), OperatorNoun), key)
        {
            Position = tree.Position,
            HasChildren = true,
            Children = [CreateExpression(history, tree, tree.Root, key)]
        };
    }

    private static TraceSummaryNode CreateExpression(MemoryHistoryViewModel history,
                                                     TraceExpressionTree tree,
                                                     TraceExpressionNode node,
                                                     string parentKey)
    {
        var key = $"{parentKey}/{node.Address:X}";

        var children = node.Children.Select(c => CreateExpression(history, tree, c, key)).ToList();

        return new TraceSummaryNode(node.ClassName is { } name ? TypeNameText.Shorten(name) : UnknownOperator,
                                    GetAddressText(node.Address),
                                    key)
        {
            Block = history.Store.GetBlock(node.Address, tree.Position),
            Address = node.Address,
            Position = tree.Position,
            HasChildren = children.Count > 0,
            Children = children
        };
    }

    private static int Count(TraceExpressionNode node) => 1 + node.Children.Sum(Count);

    private static TraceSummaryNode CreateGroup(MemoryHistoryViewModel history,
                                                TraceCompileStep step,
                                                List<TraceCompileObject> objects,
                                                string parentKey)
    {
        var key = $"{parentKey}/class/{objects[0].ClassName}";

        return new TraceSummaryNode(TypeNameText.Shorten(objects[0].ClassName), GetCountText(objects.Count, ObjectNoun), key)
        {
            HasChildren = true,
            Children = [.. objects.Select(o => CreateObject(history, step, o, key))]
        };
    }

    private static TraceSummaryNode CreateObject(MemoryHistoryViewModel history,
                                                 TraceCompileStep step,
                                                 TraceCompileObject item,
                                                 string parentKey)
    {
        var end = step.Span.To.Position;

        return new TraceSummaryNode(TypeNameText.Shorten(item.ClassName),
                                    GetAddressText(item.Address),
                                    $"{parentKey}/object/{item.Address:X}")
        {
            Block = item.Block,
            Address = item.Address,
            Position = history.Store.IsLive(item.Block, end) ? end : item.Position
        };
    }
}
