using System.Globalization;
using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.Events.Parsers.Xml;

/// <summary>
/// Parses call stack frames specifically for the event XML call stack format
/// </summary>
internal static class XmlCallStackParser
{
    private const string FrameStart = "<frame";

    private const string TagEnd = ">";

    public static List<CallstackFrame> ParseCallStack(ReadOnlySpan<char> encoded, StringInternPool strings)
    {
        var frames = new List<CallstackFrame>();
        var i = 0;

        ReadOnlySpan<char> frameStart = FrameStart;
        ReadOnlySpan<char> tagEnd = TagEnd;

        while (i < encoded.Length)
        {
            var offset = encoded[i..].IndexOf(frameStart, StringComparison.Ordinal);

            if (offset < 0)
            {
                break;
            }

            i += offset;

            var relativeEnd = encoded[i..].IndexOf(tagEnd, StringComparison.Ordinal);

            if (relativeEnd < 0)
            {
                break;
            }

            var tag = encoded.Slice(i, relativeEnd);

            var module = XmlEventAttributeParser.GetAttribute(tag, "module");

            if (!module.IsEmpty)
            {
                var addressSpan = XmlEventAttributeParser.GetAttribute(tag, "address");

                var pdb = XmlEventAttributeParser.GetAttribute(tag, "pdb");

                var guid = XmlEventAttributeParser.GetAttribute(tag, "guid");

                var ageSpan = XmlEventAttributeParser.GetAttribute(tag, "age");

                var rvaSpan = XmlEventAttributeParser.GetAttribute(tag, "rva");

                int.TryParse(ageSpan, out var age);

                frames.Add(new CallstackFrame
                {
                    Module = strings.Intern(module),
                    Address = ParseAddress(addressSpan),
                    Pdb = strings.Intern(pdb),
                    Guid = strings.Intern(guid),
                    Age = age,
                    Rva = ParseRva(rvaSpan)
                });
            }

            i += relativeEnd + TagEnd.Length;
        }

        return frames;
    }

    private static ulong ParseAddress(ReadOnlySpan<char> span)
        => span.Length > 2 && span[1] is 'x' or 'X'
           && ulong.TryParse(span[2..], NumberStyles.HexNumber, null, out var value)
            ? value
            : 0UL;

    private static uint ParseRva(ReadOnlySpan<char> span)
    {
        if (span.Length > 2 && span[1] is 'x' or 'X')
        {
            return uint.TryParse(span[2..], NumberStyles.HexNumber, null, out var hex) ? hex : 0U;
        }

        return uint.TryParse(span, out var dec) ? dec : 0U;
    }
}
