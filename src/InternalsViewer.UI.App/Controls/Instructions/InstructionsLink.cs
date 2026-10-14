using System;

namespace InternalsViewer.UI.App.Controls.Instructions;

public enum InstructionsLinkType
{
    Page,
    ToggleOption,
    OpenView,
    External
}

public readonly record struct InstructionsLink(InstructionsLinkType Type, string Target)
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
                link = new InstructionsLink(InstructionsLinkType.Page, uri.AbsolutePath);
                return true;

            case "option":
                link = new InstructionsLink(InstructionsLinkType.ToggleOption, uri.AbsolutePath);
                return true;

            case "view":
                link = new InstructionsLink(InstructionsLinkType.OpenView, uri.AbsolutePath);
                return true;

            case "http":
            case "https":
                link = new InstructionsLink(InstructionsLinkType.External, uri.AbsoluteUri);
                return true;

            default:
                return false;
        }
    }
}
