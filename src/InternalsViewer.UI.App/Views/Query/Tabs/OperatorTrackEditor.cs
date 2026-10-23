using System;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.UI.App.Controls.Dialogs;
using InternalsViewer.UI.App.Services.Query.Trace;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

internal static class OperatorTrackEditor
{
    public static async Task<TraceTickDefinition?> ShowAsync(XamlRoot root,
                                                             QueryViewModel viewModel,
                                                             TraceTickDefinition? existing,
                                                             CallStackNode? node)
    {
        var functions = viewModel.GetFunctionChoices();

        var function = existing is not null
            ? functions.FirstOrDefault(f => IsFunction(f.Module, f.Symbol, existing.Module, existing.Function))
            : node?.Frame is { } frame
                ? functions.FirstOrDefault(f => IsFunction(f.Module, f.Symbol, frame.Module, node.Symbol))
                : null;

        var colour = existing is not null
            ? TraceTickTracks.ParseColour(existing.Colour)
            : node is not null
                ? TraceTickTracks.GetCategoryColour(node)
                : TraceTickTracks.DefaultColour;

        var source = new OperatorTrackDialogSource(functions, viewModel.GetOperatorChoices(), viewModel.GetLayoutAsync);

        var dialog = new OperatorTrackDialog(source,
                                             existing,
                                             function,
                                             Color.FromArgb(0xFF, colour.Red, colour.Green, colour.Blue))
        {
            XamlRoot = root,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        await dialog.LoadAsync();

        return await dialog.ShowAsync() == ContentDialogResult.Primary ? dialog.Definition : null;
    }

    private static bool IsFunction(string module, string symbol, string otherModule, string otherSymbol)
        => string.Equals(module, otherModule, StringComparison.OrdinalIgnoreCase) && symbol == otherSymbol;
}
