using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Locks;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.TransactionLog;

namespace InternalsViewer.Query.Events;

/// <summary>
/// Event XML Parser
/// </summary>
/// <remarks>
/// This uses custom XML parsing for efficiency - reading a large number of XML events creates string allocations that
/// slow the application due to GC pauses.
/// </remarks>
internal sealed class EventParser
{
    private readonly DatabaseSource? _database;

    private readonly PlanHandleRegistry _planHandles;

    // Reused for every event
    private readonly Dictionary<string, object?> _data = new();

    private readonly Dictionary<string, object?> _actions = new();

    private readonly EventResult _result;

    // Scratch for decoding the occasional value that contains XML entities (e.g. sql_text)
    private readonly StringBuilder _decodeBuffer = new();

    // Event and field names come from a small fixed vocabulary, so they're interned: each distinct name is
    // turned into a string once and that instance is reused for every later occurrence (and shared by the
    // EngineEvents that carry it), rather than allocating a new string per event.
    private readonly Dictionary<string, string> _namePool = new(StringComparer.Ordinal);

    public EventParser(DatabaseSource? database, PlanHandleRegistry planHandles)
    {
        _database = database;
        _planHandles = planHandles;

        _result = new EventResult { Name = string.Empty, Data = _data, Actions = _actions };
    }

    public EngineEvent? ParseEvent(string xml) => ParseEvent(xml.AsSpan());

    public EngineEvent? ParseEvent(ReadOnlySpan<char> xml)
    {
        if (!PopulateResult(xml))
        {
            return null;
        }

        if (_result.Actions.TryGetValue("sql_text", out var value)
            && value is string sql
            && sql.StartsWith("ALTER EVENT SESSION"))
        {
            return null;
        }

        return ToEngineEvent(_result, _database, _planHandles);
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

        var eventTagEnd = FindTagEnd(xml, eventStart);

        if (eventTagEnd < 0)
        {
            return false;
        }

        var openTag = xml[eventStart..(eventTagEnd + 1)];

        var name = GetAttribute(openTag, "name");
        var timestamp = GetAttribute(openTag, "timestamp");

        if (name.IsEmpty || timestamp.IsEmpty)
        {
            return false;
        }

        _result.Name = Intern(name);
        _result.Timestamp = DateTime.Parse(timestamp);

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

            var tagEnd = FindTagEnd(xml, i);

            if (tagEnd < 0)
            {
                break;
            }

            if (!isData && !isAction)
            {
                i = tagEnd + 1;

                continue;
            }

            var fieldName = GetAttribute(xml[i..(tagEnd + 1)], "name");

            string? value = null;
            int next;

            if (xml[tagEnd - 1] == '/')
            {
                // Self-closing <data .../> - no value.
                next = tagEnd + 1;
            }
            else
            {
                var endTag = isData ? "</data>" : "</action>";

                var endRel = xml[(tagEnd + 1)..].IndexOf(endTag.AsSpan(), StringComparison.Ordinal);

                if (endRel < 0)
                {
                    break;
                }

                var endAbs = tagEnd + 1 + endRel;

                value = ReadValue(xml[(tagEnd + 1)..endAbs]);

                next = endAbs + endTag.Length;
            }

            (isData ? _data : _actions)[Intern(fieldName)] = value;

            i = next;
        }

        return true;
    }

    private string? ReadValue(ReadOnlySpan<char> content)
    {
        var valueStart = content.IndexOf("<value".AsSpan(), StringComparison.Ordinal);

        if (valueStart < 0)
        {
            return null;
        }

        var tagEnd = content[valueStart..].IndexOf('>');

        if (tagEnd < 0)
        {
            return null;
        }

        tagEnd += valueStart;

        // Self-closing <value/>.
        if (content[tagEnd - 1] == '/')
        {
            return string.Empty;
        }

        var inner = content[(tagEnd + 1)..];

        var endOffset = inner.IndexOf("</value>".AsSpan(), StringComparison.Ordinal);

        if (endOffset < 0)
        {
            return null;
        }

        return Decode(inner[..endOffset]);
    }

    /// <summary>
    /// Find the index of the XML end tag
    /// </summary>
    private static int FindTagEnd(ReadOnlySpan<char> xml, int tagStart)
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

    private static ReadOnlySpan<char> GetAttribute(ReadOnlySpan<char> tag, string attribute)
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

            // Require an attribute boundary before the name and `="` after it, so "name" matches the attribute and
            // not a substring of another (e.g. a value).
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

    private string Decode(ReadOnlySpan<char> s)
    {
        if (s.IndexOf('&') < 0)
        {
            return s.ToString();
        }

        _decodeBuffer.Clear();

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];

            if (c != '&')
            {
                _decodeBuffer.Append(c);
                continue;
            }

            var semi = s[i..].IndexOf(';');
            if (semi < 0)
            {
                _decodeBuffer.Append(c);
                continue;
            }

            var entity = s.Slice(i + 1, semi - 1);

            switch (entity)
            {
                case "lt":
                    _decodeBuffer.Append('<');
                    break;
                case "gt":
                    _decodeBuffer.Append('>');
                    break;
                case "amp":
                    _decodeBuffer.Append('&');
                    break;
                case "quot":
                    _decodeBuffer.Append('"');
                    break;
                case "apos":
                    _decodeBuffer.Append('\'');
                    break;
                default:
                {
                    if (entity.Length > 1 
                        && entity[0] == '#' 
                        && TryDecodeNumeric(entity, out var ch)) _decodeBuffer.Append(ch);
                    else
                    {
                        // Unknown entity - leave it verbatim rather than dropping characters.
                        _decodeBuffer.Append(s.Slice(i, semi + 1));
                    }

                    break;
                }
            }

            i += semi;
        }

        return _decodeBuffer.ToString();
    }

    private static bool TryDecodeNumeric(ReadOnlySpan<char> entity, out char ch)
    {
        ch = '\0';

        var ok = entity[1] is 'x' or 'X'
            ? int.TryParse(entity[2..], System.Globalization.NumberStyles.HexNumber, null, out var code)
            : int.TryParse(entity[1..], out code);

        if (!ok || code is < 0 or > char.MaxValue)
        {
            return false;
        }

        ch = (char)code;

        return true;
    }

    private static EngineEvent ToEngineEvent(EventResult e, DatabaseSource? database, PlanHandleRegistry planHandles)
    {
        var engineEvent = e.Name switch
        {
            var n when n.Contains("file_") || n.Contains("physical_page")
                => MapIoEvent(e),
            var n when n.Contains("page") 
                => MapPageEvent(e),
            var n when n.Contains("lock_") 
                => MapLock(e),
            var n when n.Contains("wait") 
                => MapWait(e),
            "query_thread_profile" 
                => MapQueryThread(e),
            "query_memory_grant_usage" 
                => MapMemory(e),
            "transaction_log" 
                => MapTransactionLogEvent(e),
            "sql_batch_starting"
                => MapBatchStart(e),
            _ => new EngineEvent
            {
                Name = e.Name,
                Timestamp = e.Timestamp
            }
        };

        engineEvent.SequenceId = e.SequenceId;

        if (e.Actions.TryGetValue("plan_handle", out var planHandle) && planHandle is not null)
        {
            engineEvent.PlanHandleId = planHandles.GetOrAdd(planHandle.ToString());
        }

        if (database is null)
        {
            return engineEvent;
        }

        if (engineEvent is { ObjectId: 0, PageAddress: not null })
        {
            var allocationUnit = database.FindPageAllocationUnit(engineEvent.PageAddress.Value);

            engineEvent.ObjectId = allocationUnit?.ObjectId ?? 0;
            engineEvent.ObjectName = allocationUnit?.DisplayName
                                     ?? TryGetPageName(engineEvent.PageAddress.Value) ?? string.Empty;


            ApplyObjectIdentity(engineEvent, allocationUnit, includeIndex: true);
        }
        else if (engineEvent.ObjectId > 0)
        {
            var allocationUnit = database.AllocationUnits
                                         .Values
                                         .FirstOrDefault(f => f.ObjectId == engineEvent.ObjectId);

            engineEvent.ObjectName = allocationUnit?.DisplayName ?? $"(Object Id {engineEvent.ObjectId})";


            ApplyObjectIdentity(engineEvent, allocationUnit, includeIndex: false);
        }
        else if (engineEvent is TransactionLogEvent { AllocationUnitId: > 0 } logEvent)
        {
            var allocationUnit = database.AllocationUnits.TryGetValue(logEvent.AllocationUnitId, out var value)
                                 ? value
                                 : AllocationUnit.Unknown;
     
            engineEvent.ObjectId = allocationUnit?.ObjectId ?? 0;
            engineEvent.ObjectName = allocationUnit?.DisplayName ?? string.Empty;

            ApplyObjectIdentity(engineEvent, allocationUnit, includeIndex: true);
        }

        engineEvent.Category = EventCategoryClassifier.GetCategory(engineEvent);

        return engineEvent;
    }

    private static BatchStartEvent MapBatchStart(EventResult e)
    {
        return new BatchStartEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            SqlText = e.GetString("batch_text")
        };
    }

    private static void ApplyObjectIdentity(EngineEvent engineEvent, AllocationUnit? allocationUnit, bool includeIndex)
    {
        if (allocationUnit is null)
        {
            return;
        }

        engineEvent.SchemaName = allocationUnit.SchemaName;
        engineEvent.TableName = allocationUnit.TableName;

        if (includeIndex)
        {
            engineEvent.IndexName = allocationUnit.IndexName;
        }
    }

    private static string? TryGetPageName(PageAddress pageAddress)
    {
        switch (pageAddress.PageId)
        {
            case 0:
                return "File Header";
            case 9:
                return "Boot page";
            default:
                if (PageHelpers.IsBcm(pageAddress.PageId))
                {
                    return "BCM";
                }

                if (PageHelpers.IsDcm(pageAddress.PageId))
                {
                    return "DCM";
                }

                if (PageHelpers.IsGam(pageAddress.PageId))
                {
                    return "GAM";
                }

                if (PageHelpers.IsSgam(pageAddress.PageId))
                {
                    return "SGAM";
                }

                if (PageHelpers.IsPfs(pageAddress.PageId))
                {
                    return "PFS";
                }

                return null;
        }
    }
    
    private static EngineEvent MapMemory(EventResult e)
    {
        return new MemoryEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            UsedMemoryKb = e.GetLong("used_memory_kb") ?? 0,
            GrantedMemoryKb = e.GetLong("granted_memory_kb") ?? 0,
            DurationUs = e.GetLong("duration") ?? 0
        };
    }

    private static EngineEvent MapWait(EventResult e)
    {
        var waitType = (WaitType)(e.GetInt("wait_type") ?? 0);

        return new WaitEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            WaitType = waitType,
            DurationUs = e.GetLong("duration") ?? 0
        };
    }

    private static EngineEvent MapQueryThread(EventResult e)
    {
        var threadId = (e.GetInt("thread_id") ?? 0);
        var nodeId = (e.GetInt("node_id") ?? 0);

        return new QueryThreadEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            ThreadId = threadId,
            NodeId = nodeId,
            DurationUs = e.GetLong("total_time_us") ?? 0
        };
    }

    private static EngineEvent MapLock(EventResult e)
    {
        var lockMode = (LockMode)(e.GetInt("mode") ?? 0);
        var resourceType = (LockResourceType)(e.GetInt("resource_type") ?? 0);

        var resource0 = e.GetUlong("resource_0") ?? 0;
        var resource1 = e.GetUlong("resource_1") ?? 0;
        var resource2 = e.GetUlong("resource_2") ?? 0;

        var lockEvent = new LockEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            LockMode = lockMode,
            ResourceType = (LockResourceType)(e.GetInt("resource_type") ?? 0),
            ObjectId = e.GetInt("object_id") ?? 0
        };

        return resourceType switch
        {
            LockResourceType.Page =>
                lockEvent with
                {
                    PageAddress = new PageAddress((short)resource0, (int)resource1)
                },
            LockResourceType.Rid =>
                lockEvent with
                {
                    RowIdentifier = new RowIdentifier((short)resource0, (int)resource1, (ushort)resource2),
                },
            LockResourceType.Key =>
                lockEvent with
                {
                    KeyHash = $"({resource0:x})"
                },

            _ => lockEvent
        };
    }

    private static PageEvent MapPageEvent(EventResult e)
    {
        var location = e.GetUlong("page_location") ?? 0;

        var fileId = (short)(location >> 32);

        var rawPageId = (uint)(location & 0xFFFFFFFF);

        var pageId = rawPageId <= int.MaxValue ? (int)rawPageId : 0;

        return new PageEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            PageAddress = new PageAddress(fileId, pageId),
            Type = e.GetString("type")
        };
    }

    private static IoEvent MapIoEvent(EventResult e)
    {
        var offset = e.GetLong("offset") ?? 0;

        var fileId = e.GetShort("file_id") ?? 0;

        var pageId = e.GetInt("page_id") ?? (int)(offset / 8192);

        return new IoEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            PageAddress = new PageAddress(fileId, pageId),
            IsRead = e.Name?.Contains("read") ?? false
        };
    }

    private static TransactionLogEvent MapTransactionLogEvent(EventResult e)
    {
        return new TransactionLogEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            Operation = (LogOperation)(e.GetInt("operation") ?? 0),
            Context = (LogContext)(e.GetInt("context") ?? 0),
            AllocationUnitId = e.GetLong("alloc_unit_id") ?? 0
        };
    }
}