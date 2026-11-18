using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Compilation.Enums;
using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Compilation;

public static class TraceCompilationReader
{
    private const string GrammarPrefix = "SqlSemantics::DoSemantics__";

    private const string RuleSeparator = "__";

    private const string MemberSeparator = "::";

    private const string TokenSymbol = "CLexer::LGetToken";

    private const string TokenKindSymbol = "CLexer::GetTokenType";

    private const string TokenOffsetSymbol = "CParser::Event_GettingTokenType";

    private const string TextSymbol = "CSql::Init";

    private const int OffsetSlot = 1;

    private const int OffsetLead = sizeof(char);

    private const int AllocationDepth = 4;

    private static readonly HashSet<string> BindMethods = ["Bind", "BindTree", "BindScalarTree", "BindSelf", "BindTop", "BindTopArgs"];

    private static readonly HashSet<string> BindWrappers = ["COptExpr", "CScaOpArg", "COpArg"];

    private static readonly (string Name, string Symbol, int Slot, bool IsEnd)[] TreePoints =
    [
        ("Parse Tree", "CRelOp_Query::FAlgebrizeQuery", 1, false),
        ("Bound Tree", "CRelOp_Query::FAlgebrizeQuery", 1, true),
        ("Optimizer Input", "COptContext::PcxteOptimizeQuery", 1, false),
        ("Optimizer Output", "COptExpr::PqteConvertTree", 0, false)
    ];

    public static TraceCompilation Read(TraceTimeline timeline, MemoryHistoryStore store, TraceCallStore calls, AddressMap map)
    {
        var text = FindText(store, calls);

        var steps = Nest(ReadSteps(timeline));

        Place(steps, map.GetEntries()
                        .Where(e => e is { Operation: AddressMapOperation.Constructed, ClassName: not null })
                        .Select(e => (Entry: e, Block: store.GetBlock(e.Address, e.FromPosition)))
                        .Where(o => o.Block >= 0 && !store.Blocks[o.Block].IsStack)
                        .Select(o => (o.Entry.Thread,
                                      o.Entry.FromPosition,
                                      new TraceCompileObject(o.Entry.ClassName!, o.Entry.Address, o.Block, o.Entry.FromPosition))));

        PlaceTrees(steps, ReadTrees(timeline, store, map));

        return new TraceCompilation(text, ReadTokens(timeline, text?.Text), steps);
    }

    internal static (string Name, string? Detail)? GetStepName(string symbol)
    {
        if (symbol.StartsWith(GrammarPrefix, StringComparison.Ordinal))
        {
            var parts = symbol[GrammarPrefix.Length..].Split(RuleSeparator);

            return (GetTitle(parts[0]), parts.Length > 1 ? GetTitle(parts[1]) : null);
        }

        var separator = symbol.LastIndexOf(MemberSeparator, StringComparison.Ordinal);

        if (separator <= 0)
        {
            return null;
        }

        var className = symbol[..separator];

        var method = symbol[(separator + MemberSeparator.Length)..];

        return BindMethods.Contains(method) && !BindWrappers.Contains(className) ? (className, method) : null;
    }

    internal static List<TraceCompileStep> Nest(IEnumerable<TraceCompileStep> steps)
    {
        var roots = new List<TraceCompileStep>();

        var open = new Stack<TraceCompileStep>();

        foreach (var step in steps.OrderBy(s => s.ThreadId)
                                  .ThenBy(s => s.Span.From.Position)
                                  .ThenByDescending(s => s.Span.To.Position))
        {
            while (open.TryPeek(out var outer) && !outer.Contains(step.ThreadId, step.Span.From.Position))
            {
                open.Pop();
            }

            (open.TryPeek(out var parent) ? parent.Steps : roots).Add(step);

            open.Push(step);
        }

        return roots;
    }

    internal static void Place(List<TraceCompileStep> roots,
                               IEnumerable<(uint Thread, TracePosition Position, TraceCompileObject Item)> objects)
    {
        var placed = new Dictionary<TraceCompileStep, HashSet<ulong>>();

        foreach (var (thread, position, item) in objects.OrderBy(o => o.Position))
        {
            if (Find(roots, thread, position) is not { } step)
            {
                continue;
            }

            if (!placed.TryGetValue(step, out var addresses))
            {
                addresses = [];

                placed[step] = addresses;
            }

            if (addresses.Add(item.Address))
            {
                step.Objects.Add(item);
            }
        }
    }

    internal static void PlaceTrees(List<TraceCompileStep> roots,
                                    IEnumerable<(uint Thread, TracePosition Position, TraceExpressionTree Tree)> trees)
    {
        foreach (var (thread, position, tree) in trees)
        {
            Find(roots, thread, position)?.Trees.Add(tree);
        }
    }

    private static IEnumerable<(uint Thread, TracePosition Position, TraceExpressionTree Tree)> ReadTrees(TraceTimeline timeline,
                                                                                                         MemoryHistoryStore store,
                                                                                                         AddressMap map)
    {
        foreach (var (name, symbol, slot, isEnd) in TreePoints)
        {
            foreach (var (threadId, span) in GetOutermostCalls(timeline, symbol))
            {
                if (timeline.GetNode(span.CallNodeIndex) is not { } node || timeline.GetCall(node, span.CallIndex) is not { } call)
                {
                    continue;
                }

                var position = isEnd ? span.To.Position : span.From.Position;

                var root = TraceExpressionReader.Read(new MemoryHistoryReader(store, position),
                                                      a => GetClassName(map, a, position),
                                                      call.GetSlot(slot));

                if (root is not null)
                {
                    yield return (threadId, span.From.Position, new TraceExpressionTree(name, position, root));
                }
            }
        }
    }

    private static IEnumerable<(uint ThreadId, TraceSpan Span)> GetOutermostCalls(TraceTimeline timeline, string symbol)
    {
        (uint ThreadId, TraceSpan Span)? outer = null;

        foreach (var call in timeline.GetCalls(symbol).OrderBy(c => c.ThreadId).ThenBy(c => c.Span.From.Position))
        {
            if (outer is { } last && last.ThreadId == call.ThreadId && call.Span.From.Position < last.Span.To.Position)
            {
                continue;
            }

            outer = call;

            yield return call;
        }
    }

    private static string? GetClassName(AddressMap map, ulong address, TracePosition position)
        => map.GetEntry(address, position) is { } entry ? entry.VtableClassName ?? entry.ClassName : null;

    private static TraceCompileStep? Find(List<TraceCompileStep> steps, uint thread, TracePosition position)
        => steps.Find(s => s.Contains(thread, position)) is { } found ? Find(found.Steps, thread, position) ?? found : null;

    private static IEnumerable<TraceCompileStep> ReadSteps(TraceTimeline timeline)
    {
        foreach (var stage in TraceStage.Build(timeline))
        {
            yield return new TraceCompileStep(stage.Name, null, stage.Node, stage.ThreadId, stage.Span);
        }

        foreach (var (threadId, span) in timeline.GetCalls(n => GetStepName(n.Symbol) is not null))
        {
            if (timeline.GetNode(span.CallNodeIndex) is { } node && GetStepName(node.Symbol) is { } step)
            {
                yield return new TraceCompileStep(step.Name, step.Detail, node, threadId, span);
            }
        }
    }

    private static TraceCompileText? FindText(MemoryHistoryStore store, TraceCallStore calls)
    {
        TraceCompileText? found = null;

        for (var block = 0; block < store.Blocks.Count; block++)
        {
            var item = store.Blocks[block];

            if (item.IsStack || item.WriteCount == 0 || !IsAllocatedIn(calls, item.CallNodeIndex))
            {
                continue;
            }

            if (MemoryHistoryTextReader.GetText(store, block) is { } text && text.Length > (found?.Text.Length ?? 0))
            {
                found = new TraceCompileText(block, item.Address, item.Size, text, store.GetWrites(block)[^1].Position);
            }
        }

        return found;
    }

    private static bool IsAllocatedIn(TraceCallStore calls, int callNodeIndex)
        => calls.GetNode(callNodeIndex)?.GetAncestors().Take(AllocationDepth).Any(n => n.Symbol == TextSymbol) == true;

    private static List<TraceToken> ReadTokens(TraceTimeline timeline, string? text)
    {
        var kinds = timeline.GetCalls(TokenKindSymbol).ToList();

        var offsets = timeline.GetCalls(TokenOffsetSymbol).ToList();

        var calls = timeline.GetCalls(TokenSymbol).OrderBy(c => c.Span.From.Position).ToList();

        var starts = calls.Select(c => FindInside(timeline, offsets, c) is { } call
                                          ? ((int)call.GetSlot(OffsetSlot) - OffsetLead) / sizeof(char)
                                          : -1)
                          .ToList();

        var tokens = new List<TraceToken>();

        for (var index = 0; index < calls.Count; index++)
        {
            var (threadId, span) = calls[index];

            if (timeline.GetNode(span.CallNodeIndex) is not { } node)
            {
                continue;
            }

            var start = starts[index];

            var end = index + 1 < starts.Count ? starts[index + 1] : text?.Length ?? start;

            var kind = FindInside(timeline, kinds, calls[index]) is { } kindCall ? (TraceTokenKind)kindCall.ReturnedValue : default;

            tokens.Add(new TraceToken(GetText(text, start, end),
                                      start,
                                      kind,
                                      (int)(timeline.GetCall(node, span.CallIndex)?.ReturnedValue ?? 0),
                                      node,
                                      span.CallIndex,
                                      span.From.Position));
        }

        return tokens;
    }

    private static TraceArgumentCall? FindInside(TraceTimeline timeline,
                                                 List<(uint ThreadId, TraceSpan Span)> calls,
                                                 (uint ThreadId, TraceSpan Span) outer)
    {
        foreach (var (threadId, span) in calls)
        {
            if (threadId == outer.ThreadId
                && outer.Span.From.Position <= span.From.Position
                && span.To.Position <= outer.Span.To.Position
                && timeline.GetNode(span.CallNodeIndex) is { } node)
            {
                return timeline.GetCall(node, span.CallIndex);
            }
        }

        return null;
    }

    private static string GetText(string? text, int start, int end)
        => text is null || start < 0 || start >= text.Length || end <= start
            ? string.Empty
            : text[start..Math.Min(end, text.Length)].Trim();

    private static string GetTitle(string words)
        => string.Join(' ', words.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(GetWord));

    private static string GetWord(string word)
    {
        var digits = word.Length;

        while (digits > 0 && char.IsAsciiDigit(word[digits - 1]))
        {
            digits--;
        }

        var letters = digits > 0 ? char.ToUpperInvariant(word[0]) + word[1..digits] : string.Empty;

        return digits < word.Length && letters.Length > 0 ? $"{letters} {word[digits..]}" : letters + word[digits..];
    }
}
