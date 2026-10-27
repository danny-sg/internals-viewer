using System.Globalization;
using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.Mcp.Formatting;

public static class Format
{
    public static string Milliseconds(long? microseconds)
        => microseconds is { } value ? (value / 1000d).ToString("F3", CultureInfo.InvariantCulture) : string.Empty;

    public static string Relative(long? timeUs, long cropStartUs)
        => timeUs is { } value ? ((value - cropStartUs) / 1000d).ToString("+0.000;-0.000", CultureInfo.InvariantCulture) : string.Empty;

    public static string Bytes(long? value) => value is { } bytes ? Bytes((double)bytes) : string.Empty;

    public static string Bytes(long value) => Bytes((double)value);

    public static string Bytes(ulong value) => Bytes((double)value);

    public static string Bytes(double value)
    {
        string[] units = ["B", "KB", "MB", "GB"];

        var unit = 0;

        while (Math.Abs(value) >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;

            unit++;
        }

        return unit == 0
            ? $"{value.ToString("F0", CultureInfo.InvariantCulture)} B"
            : $"{value.ToString("F1", CultureInfo.InvariantCulture)} {units[unit]}";
    }

    public static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    public static string Number(ulong value) => value.ToString("N0", CultureInfo.InvariantCulture);

    public static string Hex(ulong value) => $"0x{value:X}";

    public static string Position(TracePosition position) => position.ToString();

    public static string Position(TracePoint point) => point.Position.ToString();

    public static string Truncate(string? text, int width)
    {
        if (string.IsNullOrEmpty(text) || width <= 0)
        {
            return string.Empty;
        }

        return text.Length <= width ? text : text[..width];
    }

    public static string CompactPages(IEnumerable<PageAddress> pages)
    {
        var parts = new List<string>();

        (short File, int First, int Last)? run = null;

        foreach (var page in pages.Distinct().OrderBy(p => p.FileId).ThenBy(p => p.PageId))
        {
            if (run is { } current && current.File == page.FileId && current.Last == page.PageId - 1)
            {
                run = (current.File, current.First, page.PageId);

                continue;
            }

            if (run is { } finished)
            {
                parts.Add(Run(finished));
            }

            run = (page.FileId, page.PageId, page.PageId);
        }

        if (run is { } last)
        {
            parts.Add(Run(last));
        }

        return string.Join(", ", parts);

        static string Run((short File, int First, int Last) run)
            => run.First == run.Last ? $"{run.File}:{run.First}" : $"{run.File}:{run.First}-{run.Last} ({run.Last - run.First + 1})";
    }

    public static string EventType(EngineEvent engineEvent) => engineEvent.GetType().Name;

    public static string EventSummary(EngineEvent engineEvent)
    {
        var description = engineEvent.Description;

        var name = engineEvent.Name;

        if (description.Length > 0 && name.Length > 0 && description != name)
        {
            return description.Contains(name, StringComparison.Ordinal) ? description : $"{name}: {description}";
        }

        return description.Length > 0 ? description : name.Length > 0 ? name : EventType(engineEvent);
    }

    public static string EventLine(DocumentContext context, EngineEvent engineEvent, long cropStartUs)
        => $"[{context.Events.GetId(engineEvent)}] {Relative(engineEvent.TimeUs, cropStartUs)} " +
           $"{EventType(engineEvent)} {EventSummary(engineEvent)}";

    public static string NodeLine(DocumentContext context, CallStackNode node)
    {
        var parts = new List<string> { $"[{context.Nodes.GetId(node)}] {context.GetFunctionName(node)}" };

        if (node.Calls > 0)
        {
            parts.Add($"calls={Number(node.Calls)}");
        }

        if (node.AllocatedBytes > 0)
        {
            parts.Add($"alloc={Bytes(node.AllocatedBytes)}/{Number(node.Allocations)}");
        }

        if (node.Locators is { } locators)
        {
            if (locators.PageCount > 0)
            {
                parts.Add($"pages={locators.PageCount}");
            }

            if (locators.RowCount > 0)
            {
                parts.Add($"rows={locators.RowCount}");
            }

            if (locators.ObjectCount > 0)
            {
                parts.Add($"objects={locators.ObjectCount}");
            }
        }

        if (node.Events.Count > 0)
        {
            parts.Add($"events={node.Events.Count}");
        }

        return string.Join("  ", parts);
    }

    public static string CallPath(DocumentContext context, CallStackNode node, int depth, bool includeSelf = false)
    {
        var ancestors = node.Ancestors().Skip(includeSelf ? 0 : 1).ToList();

        var shown = ancestors.Take(depth).Select(context.GetFunctionName);

        var more = ancestors.Count > depth ? $" (+{ancestors.Count - depth} more)" : string.Empty;

        return string.Join(" < ", shown) + more;
    }

    public static string Register(int register) => register switch
    {
        0 => "RCX",
        1 => "RDX",
        2 => "R8",
        3 => "R9",
        16 => "RAX",
        >= 4 and <= 7 => $"Arg{register + 1}",
        _ => $"slot {register}"
    };

    public static string Locator(Locator locator)
    {
        var where = Register(locator.Register);

        return locator.ValueType switch
        {
            LocatorValueType.Page => $"{where} page ({locator.Page.FileId}:{locator.Page.PageId})",
            LocatorValueType.Row => $"{where} row ({locator.Page.FileId}:{locator.Page.PageId}:{locator.Slot})",
            LocatorValueType.ColumnStoreObject => $"{where} columnstore {locator}",
            LocatorValueType.Text or LocatorValueType.WideText or LocatorValueType.Bindings => $"{where} {locator.ValueType} {locator}",
            _ => $"{where} {locator.ValueType} {Hex(locator.Value)}{(locator.Detail != 0 ? $" detail {Hex(locator.Detail)}" : string.Empty)}"
        };
    }

    public static string More(int total, int shown, string hint)
        => total > shown ? $"(+{total - shown} more; {hint})" : string.Empty;

    public static void AppendIfNotEmpty(this StringBuilder builder, string text)
    {
        if (text.Length > 0)
        {
            builder.AppendLine(text);
        }
    }
}
