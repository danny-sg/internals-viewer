using System;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

public sealed partial class MemoryReferenceGraphView : UserControl, IDisposable
{
    public MemoryReferenceGraphView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public MemoryReferenceGraphViewModel? ViewModel => DataContext as MemoryReferenceGraphViewModel;

    public void Dispose() => GraphControl.Dispose();

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        sender.ItemsSource = ViewModel?.Search(sender.Text) ?? [];
    }

    private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var result = args.ChosenSuggestion as MemoryReferenceGraphSearchResult
                     ?? (ViewModel?.Search(args.QueryText) is [var first, ..] ? first : null);

        if (result is null)
        {
            return;
        }

        sender.IsSuggestionListOpen = false;

        GraphControl.ShowNode(result.Node);
    }
}
