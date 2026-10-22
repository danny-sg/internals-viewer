namespace InternalsViewer.UI.App.Controls.HexView;

public static class HexTipPin
{
    private const string PinGlyph = "";

    private const string UnpinGlyph = "";

    public static string GetGlyph(bool isPinned) => isPinned ? UnpinGlyph : PinGlyph;

    public static string GetLabel(bool isPinned) => isPinned ? "Unpin" : "Pin";
}
