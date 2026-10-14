using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls;

public static class StringText
{
    private const string TruncatedSuffix = " (Truncated)";

    public static void AddRuns(InlineCollection inlines, StringValue value)
    {
        inlines.Add(new Run { Text = value.Text, Foreground = AppResources.Get<Brush>("SqlLiteralBrush") });

        if (value.IsTruncated)
        {
            inlines.Add(new Run { Text = TruncatedSuffix, Foreground = AppResources.Get<Brush>("TextFillColorSecondaryBrush") });
        }
    }
}
