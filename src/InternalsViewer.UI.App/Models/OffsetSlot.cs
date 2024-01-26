namespace InternalsViewer.UI.App.Models;

public class OffsetSlot
{
    public short Index { get; init; }

    public ushort Offset { get; init; }

    public string Description { get; init; } = string.Empty;
}