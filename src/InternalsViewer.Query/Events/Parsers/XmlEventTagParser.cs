namespace InternalsViewer.Query.Events.Parsers;

internal sealed class XmlEventTagParser
{

    /// <summary>
    /// Find the index of the XML end tag
    /// </summary>
    public static int FindTagEnd(ReadOnlySpan<char> xml, int tagStart)
    {
        var inQuote = false;

        for (var i = tagStart; i < xml.Length; i++)
        {
            var c = xml[i];

            if (c == '"')
            {
                inQuote = !inQuote;
            }
            else if (c == '>' && !inQuote)
            {
                return i;
            }
        }

        return -1;
    }

}