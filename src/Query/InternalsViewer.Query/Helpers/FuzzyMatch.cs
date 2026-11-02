namespace InternalsViewer.Query.Helpers;

public static class FuzzyMatch
{
    public static bool IsMatch(string text, string word)
        => text.Contains(word, StringComparison.OrdinalIgnoreCase) || IsSubsequence(text, word);

    public static bool IsSubsequence(string value, string word)
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
