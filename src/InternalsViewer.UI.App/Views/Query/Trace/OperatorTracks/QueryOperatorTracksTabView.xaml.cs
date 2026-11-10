using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.UI.App.ViewModels.Query.Trace.OperatorTracks;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.OperatorTracks;

public sealed partial class QueryOperatorTracksTabView : UserControl
{
    public QueryOperatorTracksTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnDataContextChanged();

        Loaded += (_, _) => Attach(ViewModel?.OperatorTracks);

        Unloaded += (_, _) => Attach(null);
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private OperatorTracksViewModel? Attached { get; set; }

    private void OnDataContextChanged()
    {
        Bindings.Update();

        Attach(ViewModel?.OperatorTracks);
    }

    private void Attach(OperatorTracksViewModel? tracks)
    {
        if (Attached is not null)
        {
            Attached.EditRequested -= OnEditRequested;
        }

        Attached = tracks;

        if (Attached is not null)
        {
            Attached.EditRequested += OnEditRequested;
        }
    }

#pragma warning disable VSTHRD100
    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel && await OperatorTrackEditor.ShowAsync(XamlRoot, viewModel, null, null) is { } definition)
        {
            viewModel.AddTickTrack(definition);
        }
    }

    private async void OnAddWatchClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel
            && await OperatorTrackEditor.ShowAsync(XamlRoot, viewModel, null, null, isWatch: true) is { } definition)
        {
            viewModel.AddTickTrack(definition);
        }
    }

    private async void OnEditRequested(TraceTrackRule existing)
    {
        if (ViewModel is { } viewModel && await OperatorTrackEditor.ShowAsync(XamlRoot, viewModel, existing, null) is { } definition)
        {
            viewModel.ReplaceTickTrack(existing, definition);
        }
    }
#pragma warning restore VSTHRD100
}
