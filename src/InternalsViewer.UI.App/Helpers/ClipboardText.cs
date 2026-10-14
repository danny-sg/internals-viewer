using Windows.ApplicationModel.DataTransfer;

namespace InternalsViewer.UI.App.Helpers;

public static class ClipboardText
{
    public static void Copy(string text)
    {
        var package = new DataPackage();

        package.SetText(text);

        Clipboard.SetContent(package);
    }
}
