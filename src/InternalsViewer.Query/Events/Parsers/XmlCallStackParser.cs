using System.Globalization;
using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.Events.Parsers;

/// <summary>
/// Parses call stack frames from the XML-escaped callstack action value
/// </summary>
/// <remarks>
/// The callstack arrives as escaped XML held in the event buffer - each frame is <c>&amp;lt;frame .../&amp;gt;</c> with
/// its attribute quotes left literal. Parsing that span in place avoids decoding the whole (often multi-KB) callstack to
/// a string per event, which is the dominant string allocation when loading hundreds of thousands of events.
/// </remarks>
public static class XmlCallStackParser
{
    private const string FrameStart = "&lt;frame";

    private const string TagEnd = "&gt;";

    public static List<CallstackFrame> ParseCallstack(ReadOnlySpan<char> encoded, StringInternPool strings)
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

            // Frame attributes never contain a literal '>', so the first escaped close terminates the tag.
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
