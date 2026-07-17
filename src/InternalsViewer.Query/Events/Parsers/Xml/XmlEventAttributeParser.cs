namespace InternalsViewer.Query.Events.Parsers.Xml;

internal sealed class XmlEventAttributeParser
{
    public static ReadOnlySpan<char> GetAttribute(ReadOnlySpan<char> tag, string attribute)
    {
        var from = 0;

        while (true)
        {
            var offset = tag[from..].IndexOf(attribute.AsSpan(), StringComparison.Ordinal);

            if (offset < 0)
            {
                return default;
            }

            var at = from + offset;
            var after = at + attribute.Length;

            // Require an attribute boundary before the name and `="` after it, so "name" matches the attribute and not a substring of
            // another (e.g. a value)
            if (at > 0 && char.IsWhiteSpace(tag[at - 1])
                       && after + 1 < tag.Length
                       && tag[after] == '='
                       && tag[after + 1] == '"')
            {
                var valueStart = after + 2;

                var end = tag[valueStart..].IndexOf('"');

                return end < 0 ? default : tag.Slice(valueStart, end);
            }

            from = after;
        }
    }
}