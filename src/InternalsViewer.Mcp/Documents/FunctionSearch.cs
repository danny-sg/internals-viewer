using InternalsViewer.Query.CallStack;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Documents;

/// <summary>
/// A function in the call graph matched by a fuzzy search, with every node that calls it folded together
/// </summary>
public sealed record FunctionMatch(
    string Name,
    int Score,
    int Nodes,
    long Calls,
    string? Operator,
    IReadOnlyList<string> Callers,
    IReadOnlyList<CallStackNode> Members);

/// <summary>
/// Ranks the distinct functions of a call graph against free text
/// </summary>
/// <remarks>
/// Every word of the text must match. A word matches by containing it, or failing that by its letters appearing in order, so
/// a half remembered name still finds candidates. Better matches score higher, so the top of the list answers an ambiguous question.
/// </remarks>
public static class FunctionSearch
{
    public const int ExactScore = 1000;

    private const int PrefixScore = 600;

    private const int ContainsScore = 400;

    private const int SubsequenceScore = 100;

    private const int MaximumCallers = 3;

    private const int ResolveCandidates = 50;

    private const int MaximumListed = 8;

    public static IReadOnlyList<FunctionMatch> Resolve(IEnumerable<CallStackNode> nodes, string text, bool mergeBest)
    {
        var matches = Search(nodes, text, ResolveCandidates);

        if (matches.Count == 0)
        {
            throw new McpException($"No function in the call graph matches '{text}'. Try trace_find_functions with fewer words.");
        }

        var best = matches.Where(m => m.Score == matches[0].Score).ToList();

        if (best.Count == 1 || mergeBest)
        {
            return best;
        }

        throw new McpException(
            $"'{text}' is ambiguous. Candidates: {string.Join(", ", best.Take(MaximumListed).Select(m => m.Name))}. " +
            "Use a more specific name or module!name, look with trace_find_functions, or set allMatches to take them all.");
    }

    public static IReadOnlyList<FunctionMatch> Search(IEnumerable<CallStackNode> nodes, string text, int limit)
    {
        var words = text.Split([' ', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
        {
            return [];
        }

        var groups = new Dictionary<string, List<CallStackNode>>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            if (node.Frame is null || node.Symbol.Length == 0)
            {
                continue;
            }

            var name = $"{node.Frame.Module}!{node.Symbol}";

            if (!groups.TryGetValue(name, out var list))
            {
                list = [];

                groups[name] = list;
            }

            list.Add(node);
        }

        var matches = new List<FunctionMatch>();

        foreach (var (name, members) in groups)
        {
            if (Score(name, words) is not { } score)
            {
                continue;
            }

            var callers = members.Select(m => m.Parent?.Symbol)
                                 .Where(s => !string.IsNullOrEmpty(s))
                                 .Distinct()
                                 .Take(MaximumCallers)
                                 .Select(s => s!)
                                 .ToList();

            matches.Add(new FunctionMatch(name,
                                          score,
                                          members.Count,
                                          members.Sum(m => m.Calls),
                                          members.Select(m => m.Operator).FirstOrDefault(o => o is not null),
                                          callers,
                                          members));
        }

        return [.. matches.OrderByDescending(m => m.Score)
                          .ThenByDescending(m => m.Calls)
                          .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                          .Take(limit)];
    }

    private static int? Score(string name, string[] words)
    {
        var symbol = name[(name.IndexOf('!') + 1)..];

        var total = 0;

        foreach (var word in words)
        {
            var score = ScoreWord(name, symbol, word);

            if (score == 0)
            {
                return null;
            }

            total += score;
        }

        return total / words.Length;
    }

    private static int ScoreWord(string name, string symbol, string word)
    {
        if (symbol.Equals(word, StringComparison.OrdinalIgnoreCase)
            || name.Equals(word, StringComparison.OrdinalIgnoreCase)
            || symbol.EndsWith("::" + word, StringComparison.OrdinalIgnoreCase))
        {
            return ExactScore;
        }

        if (symbol.StartsWith(word, StringComparison.OrdinalIgnoreCase))
        {
            return PrefixScore;
        }

        if (name.Contains(word, StringComparison.OrdinalIgnoreCase))
        {
            return ContainsScore;
        }

        return IsSubsequence(symbol, word) ? SubsequenceScore : 0;
    }

    private static bool IsSubsequence(string value, string word)
    {
        var position = 0;

        foreach (var character in value)
        {
            if (position < word.Length && char.ToLowerInvariant(character) == char.ToLowerInvariant(word[position]))
            {
                position++;
            }
        }

        return position == word.Length;
    }
}
