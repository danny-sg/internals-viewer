using System.Globalization;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Parsing.Plans;

namespace InternalsViewer.Query.Events.Parsers;

/// <summary>
/// Event XML Parser
/// </summary>
/// <remarks>
/// This uses custom XML parsing for efficiency - reading a large number of XML events creates string allocations that
/// slow the application due to GC pauses.
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

    // Reused for every event. Values are stored as ranges into the event's character buffer, not strings,
    // so only the few that are read as strings ever allocate (see EventResultExtensions).
    private readonly Dictionary<string, ValueRange> _data = new();

    private readonly Dictionary<string, ValueRange> _actions = new();

    private readonly EventResult _result;

    // Event and field names come from a small fixed vocabulary, so they're interned: each distinct name is
    // turned into a string once and that instance is reused for every later occurrence (and shared by the
    // EngineEvents that carry it), rather than allocating a new string per event.
    private readonly Dictionary<string, string> _namePool = new(StringComparer.Ordinal);

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

        _result.Name = Intern(name);
        _result.Timestamp = DateTime.Parse(
            timestamp,
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
                // End tag: stop at </event>, otherwise skip it.
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
                // Self-closing <data .../> - no value.
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

            (isData ? _data : _actions)[Intern(fieldName)] = range;

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

        // Self-closing <value/>.
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

    private string Intern(ReadOnlySpan<char> name)
    {
        var lookup = _namePool.GetAlternateLookup<ReadOnlySpan<char>>();

        if (lookup.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var interned = name.ToString();

        _namePool[interned] = interned;

        return interned;
    }
}