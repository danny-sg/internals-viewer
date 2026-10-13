namespace InternalsViewer.Query.CallStack.Categories;

/// <summary>
/// One mapping-field pattern — exact, a <c>*</c> glob, or the pure wildcard — parsed once for cheap match + scoring
/// </summary>
/// <remarks>
/// <c>CQScan</c> is exact (case-insensitive), <c>CQScan*</c> starts-with, <c>*Scan</c> ends-with, <c>*CQScan*</c>
/// contains, and <c>*</c> matches anything. <see cref="Score"/> ranks specificity so the most detailed rule wins: an
/// exact match dominates any glob (via <see cref="ExactBonus"/>), a glob scores by how many literal characters it
/// pins, and the pure wildcard scores nothing.
/// </remarks>
public readonly struct GlobPattern
{
    private readonly string _pattern;

    /// <summary>
    /// Specificity of this pattern — higher is more specific (exact ≫ literal-length glob &gt; wildcard)
    /// </summary>
    public int Score { get; }

    /// <summary>
    /// The pattern as written in the mapping file
    /// </summary>
    public string Text => _pattern;

    /// <summary>
    /// Whether the pattern matches anything at all, being blank or the lone wildcard
    /// </summary>
    public bool IsAny => Shape == GlobShape.Any;

    internal GlobShape Shape { get; }

    internal string Literal { get; }

    // An exact match is always more specific than any glob, however long, so it starts far above literal-length scores.
    private const int ExactBonus = 1_000_000;

    private GlobPattern(string pattern, GlobShape shape, string literal, int score)
    {
        _pattern = pattern;
        Shape = shape;
        Literal = literal;
        Score = score;
    }

    public static GlobPattern Parse(string? pattern)
    {
        pattern = pattern?.Trim() ?? string.Empty;

        if (pattern.Length == 0 || pattern == "*")
        {
            return new GlobPattern(pattern, GlobShape.Any, string.Empty, score: 0);
        }

        if (!pattern.Contains('*'))
        {
            return new GlobPattern(pattern, GlobShape.Exact, pattern, score: ExactBonus + pattern.Length);
        }

        var literalLength = pattern.Count(c => c != '*');

        var (shape, literal) = ShapeOf(pattern);

        return new GlobPattern(pattern, shape, literal, score: literalLength);
    }

    public bool Matches(ReadOnlySpan<char> value)
        => Shape switch
        {
            GlobShape.Any => true,
            GlobShape.Exact => value.Equals(Literal, StringComparison.OrdinalIgnoreCase),
            GlobShape.Prefix => value.StartsWith(Literal, StringComparison.OrdinalIgnoreCase),
            GlobShape.Suffix => value.EndsWith(Literal, StringComparison.OrdinalIgnoreCase),
            GlobShape.Contains => value.Contains(Literal, StringComparison.OrdinalIgnoreCase),
            _ => GlobMatch(_pattern, value)
        };

    private static (GlobShape Shape, string Literal) ShapeOf(string pattern)
    {
        var leading = pattern[0] == '*';

        var trailing = pattern[^1] == '*';

        var literal = pattern[(leading ? 1 : 0)..(trailing ? ^1 : ^0)];

        if (literal.Length == 0 || literal.Contains('*'))
        {
            return (GlobShape.General, string.Empty);
        }

        return (leading, trailing) switch
        {
            (false, true) => (GlobShape.Prefix, literal),
            (true, false) => (GlobShape.Suffix, literal),
            _ => (GlobShape.Contains, literal)
        };
    }

    // Case-insensitive glob: '*' matches any run (including empty). Two-pointer walk with a remembered star position, so
    // no regex and no catastrophic backtracking on the simple patterns the mapping files use.
    private static bool GlobMatch(string pattern, ReadOnlySpan<char> value)
    {
        int p = 0, v = 0, star = -1, mark = 0;

        while (v < value.Length)
        {
            if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                mark = v;
            }
            else if (p < pattern.Length && EqualsIgnoreCase(pattern[p], value[v]))
            {
                p++;
                v++;
            }
            else if (star >= 0)
            {
                p = star + 1;
                v = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }

    private static bool EqualsIgnoreCase(char a, char b) =>
        a == b || char.ToUpperInvariant(a) == char.ToUpperInvariant(b);
}
