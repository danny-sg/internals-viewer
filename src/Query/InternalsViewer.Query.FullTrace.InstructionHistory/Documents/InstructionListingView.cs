using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Documents;

public sealed partial class InstructionListingView : Grid, IDisposable
{
    public InstructionListingView(InstructionListingViewModel model)
    {
        Model = model;

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Progress = new ProgressBar { IsIndeterminate = true };

        SummaryText = new TextBlock { Margin = new Thickness(8, 4, 8, 4) };

        MessageText = new TextBlock { Margin = new Thickness(8, 4, 8, 4), TextWrapping = TextWrapping.Wrap };

        Repeater = new ItemsRepeater { ItemTemplate = new InstructionLineFactory() };

        var scroller = new ScrollViewer
        {
            Content = Repeater,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Auto
        };

        SetRow(SummaryText, 1);
        SetRow(MessageText, 2);
        SetRow(scroller, 3);

        Children.Add(Progress);
        Children.Add(SummaryText);
        Children.Add(MessageText);
        Children.Add(scroller);

        Model.PropertyChanged += OnModelChanged;

        Refresh();
    }

    private InstructionListingViewModel Model { get; }

    private ProgressBar Progress { get; }

    private TextBlock SummaryText { get; }

    private TextBlock MessageText { get; }

    private ItemsRepeater Repeater { get; }

    public void Dispose() => Model.PropertyChanged -= OnModelChanged;

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        Progress.Visibility = Model.IsLoading ? Visibility.Visible : Visibility.Collapsed;

        SummaryText.Text = Model.Summary ?? string.Empty;

        SummaryText.Visibility = Model.Summary is null ? Visibility.Collapsed : Visibility.Visible;

        MessageText.Text = Model.Message ?? string.Empty;

        MessageText.Visibility = Model.Message is null ? Visibility.Collapsed : Visibility.Visible;

        if (!ReferenceEquals(Repeater.ItemsSource, Model.Lines))
        {
            Repeater.ItemsSource = Model.Lines;
        }
    }
}
