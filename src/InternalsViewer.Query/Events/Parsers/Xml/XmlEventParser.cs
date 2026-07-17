using System.Globalization;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Parsing.Plans;

namespace InternalsViewer.Query.Events.Parsers.Xml;

/// <summary>
/// Event XML Parser
/// </summary>
/// <remarks>
/// Custom minimal Span/buffer based XML parser.
///
/// Reading thousands of events at a time can cause a huge amount of string allocations. The built-in parsers aren't great in the usage
/// scenario, you'll see a lot of GC churn which does materially slow the parsing down.
///
/// This parser works directly on string buffers and assumes a limited dictionary of known element and attribute names which are
/// string-interned.
/// </remarks>
internal sealed class XmlEventParser
{
    private readonly DatabaseSource? _database;

    private readonly PlanHandleRegistry _planHandles;

    private readonly EventParser _eventParser;

    /// <summary>
    /// The shared call stack tree built from every parsed event's frames
    /// </summary>
    public CallStackTree CallStack { get; } = new();

    private readonly Dictionary<string, ValueRange> _data = new();

    private readonly Dictionary<string, ValueRange> _actions = new();

    private readonly EventResult _result;

    private readonly StringInternPool _names = new();

    public XmlEventParser(DatabaseSource? database, PlanHandleRegistry planHandles, EventParser eventParser)
    {
        _database = database;
        _planHandles = planHandles;
        _eventParser = eventParser;

        _result = new EventResult { Name = string.Empty, Data = _data, Actions = _actions };
    }

    public EngineEvent? ParseEvent(string xml)
    {
        var buffer = xml.ToCharArray();

        return ParseEvent(buffer, buffer.Length);
    }

    public EngineEvent? ParseEvent(char[] buffer, int length)
    {
        _result.Buffer = buffer;

        if (!PopulateResult(buffer.AsSpan(0, length)))
        {
            return null;
        }

        return _eventParser.ToEngineEvent(_result, _database, _planHandles, CallStack);
    }

    private bool PopulateResult(ReadOnlySpan<char> xml)
    {
        _data.Clear();
        _actions.Clear();

        var eventStart = xml.IndexOf("<event".AsSpan(), StringComparison.Ordinal);

        if (eventStart < 0)
        {
            return false;
        }

        var eventTagEnd = XmlEventTagParser.FindTagEnd(xml, eventStart);

        if (eventTagEnd < 0)
        {
            return false;
        }

        var openTag = xml[eventStart..(eventTagEnd + 1)];

        var name = XmlEventAttributeParser.GetAttribute(openTag, "name");
        var timestamp = XmlEventAttributeParser.GetAttribute(openTag, "timestamp");

        if (name.IsEmpty || timestamp.IsEmpty)
        {
            return false;
        }

        _result.Name = _names.Intern(name);

        _result.Timestamp = DateTime.Parse(timestamp,
                                           CultureInfo.InvariantCulture,
                                           DateTimeStyles.RoundtripKind);

        // Self-closing <event .../> (no fields).
        if (xml[eventTagEnd - 1] == '/')
        {
            return true;
        }

        var i = eventTagEnd + 1;

        while (i < xml.Length)
        {
            var offset = xml[i..].IndexOf('<');

            if (offset < 0)
            {
                break;
            }

            i += offset;

            if (i + 1 < xml.Length && xml[i + 1] == '/')
            {
                // End tag: stop at </event>, otherwise skip it
                if (IsElementName(xml, i + 2, "event"))
                {
                    break;
                }

                var close = xml[i..].IndexOf('>');

                if (close < 0)
                {
                    break;
                }

                i += close + 1;
                continue;
            }

            var isData = IsElementName(xml, i + 1, "data");

            var isAction = !isData && IsElementName(xml, i + 1, "action");

            var tagEnd = XmlEventTagParser.FindTagEnd(xml, i);

            if (tagEnd < 0)
            {
                break;
            }

            if (!isData && !isAction)
            {
                i = tagEnd + 1;

                continue;
            }

            var fieldName = XmlEventAttributeParser.GetAttribute(xml[i..(tagEnd + 1)], "name");

            ValueRange range = default;
            int next;

            if (xml[tagEnd - 1] == '/')
            {
                // Self-closing <data .../> - no value
                next = tagEnd + 1;
            }
            else
            {
                var endTag = isData ? "</data>" : "</action>";

                var relativeEndPosition = xml[(tagEnd + 1)..].IndexOf(endTag.AsSpan(), StringComparison.Ordinal);

                if (relativeEndPosition < 0)
                {
                    break;
                }

                var fullEndPosition = tagEnd + 1 + relativeEndPosition;

                range = ReadValueRange(xml[(tagEnd + 1)..fullEndPosition], tagEnd + 1);

                next = fullEndPosition + endTag.Length;
            }

            (isData ? _data : _actions)[_names.Intern(fieldName)] = range;

            i = next;
        }

        return true;
    }

    private static ValueRange ReadValueRange(ReadOnlySpan<char> content, int contentStart)
    {
        var valueStart = content.IndexOf("<value".AsSpan(), StringComparison.Ordinal);

        if (valueStart < 0)
        {
            return default;
        }

        var tagEndPosition = content[valueStart..].IndexOf('>');

        if (tagEndPosition < 0)
        {
            return default;
        }

        tagEndPosition += valueStart;

        // Self-closing <value/>
        if (content[tagEndPosition - 1] == '/')
        {
            return default;
        }

        var inner = content[(tagEndPosition + 1)..];

        var endOffset = inner.IndexOf("</value>".AsSpan(), StringComparison.Ordinal);

        if (endOffset < 0)
        {
            return default;
        }

        return new ValueRange(contentStart + tagEndPosition + 1, endOffset);
    }

    private static bool IsElementName(ReadOnlySpan<char> xml, int nameStart, string element)
    {
        if (nameStart + element.Length > xml.Length)
        {
            return false;
        }

        if (!xml.Slice(nameStart, element.Length).SequenceEqual(element))
        {
            return false;
        }

        var after = xml[nameStart + element.Length];

        return after is ' ' or '\t' or '\r' or '\n' or '>' or '/';
    }
}