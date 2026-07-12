namespace InternalsViewer.Query.Callstack.Categories;

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

    private readonly bool _isAny;

    private readonly bool _isExact;

    /// <summary>
    /// Specificity of this pattern — higher is more specific (exact ≫ literal-length glob &gt; wildcard)
    /// </summary>
    public int Score { get; }

    // An exact match is always more specific than any glob, however long, so it starts far above literal-length scores.
    private const int ExactBonus = 1_000_000;

    private GlobPattern(string pattern, bool isAny, bool isExact, int score)
    {
        _pattern = pattern;
        _isAny = isAny;
        _isExact = isExact;
        Score = score;
    }

    public static GlobPattern Parse(string? pattern)
    {
        pattern = pattern?.Trim() ?? string.Empty;

        if (pattern.Length == 0 || pattern == "*")
        {
            return new GlobPattern(pattern, isAny: true, isExact: false, score: 0);
        }

        if (!pattern.Contains('*'))
        {
            return new GlobPattern(pattern, isAny: false, isExact: true, score: ExactBonus + pattern.Length);
        }

        var literalLength = pattern.Count(c => c != '*');

        return new GlobPattern(pattern, isAny: false, isExact: false, score: literalLength);
    }

    public bool Matches(string? value)
    {
        if (_isAny)
        {
            return true;
        }

        value ??= string.Empty;

        return _isExact
            ? string.Equals(value, _pattern, StringComparison.OrdinalIgnoreCase)
            : GlobMatch(_pattern, value);
    }

    // Case-insensitive glob: '*' matches any run (including empty). Two-pointer walk with a remembered star position, so
    // no regex and no catastrophic backtracking on the simple patterns the mapping files use.
    private static bool GlobMatch(string pattern, string value)
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
