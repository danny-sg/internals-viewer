namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Address of a value in a variable length data store, being its page and its slot on that page
/// </summary>
/// <remarks>
/// Written the way a row identifier is, the store being addressed rather than searched because each of its pages
/// is compressed on its own and a reader wants to expand only the one holding the value it is after.
/// </remarks>
public readonly record struct SegmentPageSlot(int Page, int Slot)
{
    public override string ToString() => $"({Page}:{Slot})";

    public static bool TryParse(string? text, out SegmentPageSlot pageSlot)
    {
        pageSlot = default;

        if (text is null || text.Length < 5 || text[0] != '(' || text[^1] != ')')
        {
            return false;
        }

        var parts = text[1..^1].Split(':');

        if (parts.Length != 2 || !int.TryParse(parts[0], out var page) || !int.TryParse(parts[1], out var slot))
        {
            return false;
        }

        pageSlot = new SegmentPageSlot(page, slot);

        return true;
    }
}
