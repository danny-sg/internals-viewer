using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace InternalsViewer.UI.App.Controls.Results;

public static class HoverUnderline
{
    public static void Attach(TextBlock text)
    {
        text.PointerEntered += (_, _) => SetUnderline(text, UnderlineStyle.Single);
        text.PointerExited += (_, _) => SetUnderline(text, UnderlineStyle.None);
        text.PointerCanceled += (_, _) => SetUnderline(text, UnderlineStyle.None);
        text.PointerCaptureLost += (_, _) => SetUnderline(text, UnderlineStyle.None);
    }

    private static void SetUnderline(TextBlock text, UnderlineStyle style)
    {
        foreach (var link in text.Inlines.OfType<Hyperlink>())
        {
            link.UnderlineStyle = style;
        }
    }
}
