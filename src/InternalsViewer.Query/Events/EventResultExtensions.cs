using System.Globalization;
using System.Text;

namespace InternalsViewer.Query.Events;

public static class EventResultExtensions
{
    public static short? GetShort(this EventResult e, string key)
        => TryGetSpan(e.Data, e.Buffer, key, out var span) && short.TryParse(span, out var i) ? i : null;

    public static int? GetInt(this EventResult e, string key)
        => TryGetSpan(e.Data, e.Buffer, key, out var span) && int.TryParse(span, out var i) ? i : null;

    public static long? GetLong(this EventResult e, string key)
        => TryGetSpan(e.Data, e.Buffer, key, out var span) && long.TryParse(span, out var i) ? i : null;

    public static ulong? GetUlong(this EventResult e, string key)
        => TryGetSpan(e.Data, e.Buffer, key, out var span) && ulong.TryParse(span, out var i) ? i : null;

    public static string GetString(this EventResult e, string key)
        => TryGetSpan(e.Data, e.Buffer, key, out var span) ? Decode(span) : string.Empty;

    public static int GetDatabaseId(this EventResult e)
        => TryGetSpan(e.Actions, e.Buffer, "database_id", out var span) && int.TryParse(span, out var i) ? i : -1;

    private static bool TryGetSpan(Dictionary<string, ValueRange> source,
                                   char[] buffer,
                                   string key,
                                   out ReadOnlySpan<char> span)
    {
        if (source.TryGetValue(key, out var range) && range.Length > 0)
        {
            span = buffer.AsSpan(range.Offset, range.Length);
            return true;
        }

        span = default;
        return false;
    }

    private static string Decode(ReadOnlySpan<char> s)
    {
        if (s.IndexOf('&') < 0)
        {
            return s.ToString();
        }

        var sb = new StringBuilder(s.Length);

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];

            if (c != '&')
            {
                sb.Append(c);
                continue;
            }

            var semi = s[i..].IndexOf(';');
            if (semi < 0)
            {
                sb.Append(c);
                continue;
            }

            var entity = s.Slice(i + 1, semi - 1);

            switch (entity)
            {
                case "lt":
                    sb.Append('<');
                    break;
                case "gt":
                    sb.Append('>');
                    break;
                case "amp":
                    sb.Append('&');
                    break;
                case "quot":
                    sb.Append('"');
                    break;
                case "apos":
                    sb.Append('\'');
                    break;
                default:
                    if (entity.Length > 1 && entity[0] == '#' && TryDecodeNumeric(entity, out var ch))
                    {
                        sb.Append(ch);
                    }
                    else
                    {
                        // Unknown entity - leave it verbatim rather than dropping characters.
                        sb.Append(s.Slice(i, semi + 1));
                    }

                    break;
            }

            i += semi;
        }

        return sb.ToString();
    }

    private static bool TryDecodeNumeric(ReadOnlySpan<char> entity, out char result)
    {
        result = '\0';

        var ok = entity[1] is 'x' or 'X'
            ? int.TryParse(entity[2..], NumberStyles.HexNumber, null, out var code)
            : int.TryParse(entity[1..], out code);

        if (!ok || code is < 0 or > char.MaxValue)
        {
            return false;
        }

        result = (char)code;

        return true;
    }
}
