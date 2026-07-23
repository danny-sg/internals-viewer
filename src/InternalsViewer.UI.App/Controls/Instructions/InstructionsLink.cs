using System;

namespace InternalsViewer.UI.App.Controls.Instructions;

public enum InstructionsLinkKind
{
    Page,
    ToggleOption,
    OpenView,
    External
}

public readonly record struct InstructionsLink(InstructionsLinkKind Kind, string Target)
{
    public static bool TryParse(Uri? uri, out InstructionsLink link)
    {
        link = default;

        if (uri is null || !uri.IsAbsoluteUri)
        {
            return false;
        }

        switch (uri.Scheme)
        {
            case "guide":
                link = new InstructionsLink(InstructionsLinkKind.Page, uri.AbsolutePath);
                return true;

            case "option":
                link = new InstructionsLink(InstructionsLinkKind.ToggleOption, uri.AbsolutePath);
                return true;

            case "view":
                link = new InstructionsLink(InstructionsLinkKind.OpenView, uri.AbsolutePath);
                return true;

            case "http":
            case "https":
                link = new InstructionsLink(InstructionsLinkKind.External, uri.AbsoluteUri);
                return true;

            default:
                return false;
        }
    }
}
