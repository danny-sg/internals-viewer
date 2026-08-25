namespace InternalsViewer.UI.App.Models.Page;

public sealed record PageSlot
{
    public short Index { get; init; }

    public ushort Offset { get; init; }

    public string Description { get; init; } = string.Empty;
}